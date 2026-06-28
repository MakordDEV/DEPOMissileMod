import base64
import json
import socket
import time
import threading
import re
from flask import Flask, request, jsonify

app = Flask(__name__)
UDP_IP = "0.0.0.0"
UDP_PORT = 9999
HTTPS_PORT = 9998

BUF_SIZE = 65535  # max UDP datagram size

missiles = {}            # scene -> {ip: {...}}
photos = {}              # ip -> binary jpeg
udp_clients = set()      # (ip, port)
client_last_alive = {}   # ip -> timestamp
client_last_scene = {}   # ip -> last get_cord scene
client_nicknames = {}    # ip -> nickname

data_lock = threading.Lock()
addr_to_steam = {}

TIMEOUT_SECONDS = 30


def remove_client(addr):
    global udp_clients, photos, missiles, client_last_alive, client_last_scene, client_nicknames, addr_to_steam

    with data_lock:
        udp_clients.discard(addr)
        photos.pop(addr, None)
        client_last_alive.pop(addr, None)
        client_last_scene.pop(addr, None)
        client_nicknames.pop(addr, None)

        steam_id = addr_to_steam.get(addr)
        if steam_id:
            for scene in missiles:
                if steam_id in missiles[scene]:
                    del missiles[scene][steam_id]
            addr_to_steam.pop(addr, None)

    print(f"[Удалён] {addr} за неактивность или по 'bye'")


def imalive_watcher():
    while True:
        now = time.time()
        to_remove = []

        with data_lock:
            for addr, last_alive in list(client_last_alive.items()):
                if now - last_alive > TIMEOUT_SECONDS:
                    to_remove.append(addr)

        for addr in to_remove:
            remove_client(addr)

        time.sleep(TIMEOUT_SECONDS)


def main():
    udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_sock.bind((UDP_IP, UDP_PORT))
    print(f"UDP сервер слушает {UDP_IP}:{UDP_PORT}")

    threading.Thread(target=imalive_watcher, daemon=True).start()

    while True:
        try:
            data, addr = udp_sock.recvfrom(BUF_SIZE)
            
            with data_lock:
                udp_clients.add(addr)

            if data.startswith(b"\xFF\xD8"):
                with data_lock:
                    photos[addr] = data
                print(f"JPEG-фотография сохранена от {addr}")
                continue

            text = data.decode('utf-8', errors='ignore').strip()

            parts = text.split(";")
            if not parts:
                continue

            command = parts[0]

            if text == "bye":
                remove_client(addr)
                continue

            if command == "imalive":
                with data_lock:
                    client_last_alive[addr] = time.time()
                continue

            if command == "hello" and len(parts) >= 2:
                steam_id = parts[1].strip()
                with data_lock:
                    addr_to_steam[addr] = steam_id
                    client_nicknames[addr] = steam_id
                print(f"[Приветствие] {addr} → '{steam_id}'")
                continue

            with data_lock:
                steam_id = addr_to_steam.get(addr, f"{addr[0]}:{addr[1]}")

            if command == "launch_missile" and len(parts) >= 5:
                p_steam_id, scene, coords, skin = parts[1:5]
                with data_lock:
                    if scene not in missiles:
                        missiles[scene] = {}
                    missiles[scene][p_steam_id] = {"name": p_steam_id, "coords": coords, "skin": skin}
                print(f"[launch_missile] {addr} → '{p_steam_id}' → {scene} ; {coords} ; {skin}")

            elif command == "missile_prefab_data" and len(parts) >= 4:
                name, scene, b64 = parts[1:4]
                name = re.sub(r'[^a-zA-Z0-9_\-]', '', name)
                scene = re.sub(r'[^a-zA-Z0-9_\-]', '', scene)
                try:
                    json_data = base64.b64decode(b64).decode("utf-8")
                    prefab_info = json.loads(json_data)
                    with open(f"prefab_data/{name}_{scene}.json", "w", encoding="utf-8") as f:
                        json.dump(prefab_info, f, ensure_ascii=False, indent=2)
                    print(f"[prefab] received prefab for {name} in scene {scene}")
                except Exception as e:
                    print(f"[prefab ERROR] Failed to decode or save prefab for {name}: {e}")

            elif command == "move_missile" and len(parts) >= 3:
                scene, coords = parts[1:3]
                with data_lock:
                    if scene in missiles and steam_id in missiles[scene]:
                        missiles[scene][steam_id]["coords"] = coords
                nickname = client_nicknames.get(addr, "<неизвестно>")
                print(f"[move_missile] {addr} → '{nickname}' → {coords} ; {scene}")

            elif command == "explode_missile" and len(parts) >= 3:
                scene, coords = parts[1:3]
                with data_lock:
                    if scene in missiles and steam_id in missiles[scene]:
                        del missiles[scene][steam_id]
                nickname = client_nicknames.get(addr, "<неизвестно>")
                print(f"[explode_missile] {addr} → '{nickname}' → {coords} ; {scene}")

            elif command == "get_cord" and len(parts) >= 2:
                scene = parts[1].strip()

                with data_lock:
                    last_scene = client_last_scene.get(addr)
                    if last_scene != scene:
                        client_last_scene[addr] = scene
                        nickname = client_nicknames.get(addr, "<неизвестно>")
                        print(f"[get_cord] {addr} → '{nickname}' → сцена: {scene}")

                    if scene in missiles:
                        missile_list = []
                        for m_steam_id, data_dict in missiles[scene].items():
                            missile_list.append({
                                "name": data_dict["name"],
                                "scene": scene,
                                "coords": data_dict["coords"],
                                "skin": data_dict["skin"],
                                "ip": m_steam_id
                            })
                        msg = "missiles_json;" + json.dumps(missile_list)
                        udp_sock.sendto(msg.encode('utf-8'), addr)

        except Exception as e:
            print(f"Ошибка в основном цикле UDP: {e}")

@app.route("/get_cord", methods=["GET"])
def get_cord():
    scene = request.args.get("scene")

    with data_lock:
        if scene not in missiles:
            return jsonify([])

        missile_list = []
        for m_steam_id, data_dict in missiles[scene].items():
            missile_list.append({
                "name": data_dict["name"],
                "scene": scene,
                "coords": data_dict["coords"],
                "skin": data_dict["skin"],
                "ip": m_steam_id
            })
    return jsonify(missile_list)

if __name__ == "__main__":
    threading.Thread(target=main).start()
    context = ('/etc/letsencrypt/live/makordikr.ru/fullchain.pem', 
               '/etc/letsencrypt/live/makordikr.ru/privkey.pem')
    app.run(host='0.0.0.0', port=HTTPS_PORT, ssl_context=context)