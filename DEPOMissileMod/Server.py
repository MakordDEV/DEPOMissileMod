# Debug messages of server-side part are in Russian. Use translator to understand debug messages in your own language or figure them out from code’s functionality.
# Script uses UDP and HTTPS. If you want to run your own UDP & HTTPS server, do it on a VPS/VDS and buy certificates or get free ones from Let’s Encrypt. If you don’t want to, you can simply run an HTTP & UDP server :P

import base64
import json
import socket
import time
import threading
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

TIMEOUT_SECONDS = 30


def remove_client(ip: str):
    global udp_clients, photos, missiles, client_last_alive, client_last_scene, client_nicknames

    udp_clients = {client for client in udp_clients if client[0] != ip}
    photos.pop(ip, None)
    client_last_alive.pop(ip, None)
    client_last_scene.pop(ip, None)
    client_nicknames.pop(ip, None)

    for scene in missiles:
        if ip in missiles[scene]:
            del missiles[scene][ip]

    print(f"[Удалён] {ip} за неактивность или по 'bye'")


def imalive_watcher():
    while True:
        now = time.time()
        to_remove = []

        for ip, last_alive in client_last_alive.items():
            if now - last_alive > TIMEOUT_SECONDS:
                to_remove.append(ip)

        for ip in to_remove:
            remove_client(ip)

        time.sleep(TIMEOUT_SECONDS)


def main():
    udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_sock.bind((UDP_IP, UDP_PORT))
    print(f"UDP сервер слушает {UDP_IP}:{UDP_PORT}")

    threading.Thread(target=imalive_watcher, daemon=True).start()

    while True:
        try:
            data, addr = udp_sock.recvfrom(BUF_SIZE)
            ip, port = addr
            udp_clients.add(addr)

            if data.startswith(b"\xFF\xD8"):
                photos[ip] = data
                print(f"JPEG-фотография сохранена от {ip}")
                continue

            text = data.decode('utf-8', errors='ignore').strip()

            parts = text.split(";")
            if not parts:
                continue

            command = parts[0]

            if text == "bye":
                remove_client(ip)
                continue

            if text == "imalive":
                client_last_alive[ip] = time.time()
                continue

            if command == "hello" and len(parts) >= 2:
                nickname = parts[1].strip()
                client_nicknames[ip] = nickname
                print(f"[Приветствие] {ip} → '{nickname}'")
                continue

            if command == "launch_missile" and len(parts) >= 5:
                name, scene, coords, skin = parts[1:5]
                if scene not in missiles:
                    missiles[scene] = {}
                missiles[scene][ip] = {"name": name, "coords": coords, "skin": skin}
                print(f"[launch_missile] {ip} → '{name}' → {scene} ; {coords} ; {skin}")

            elif command == "missile_prefab_data" and len(parts) >= 3:
                name, scene, b64 = parts[1:4]
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
                if scene in missiles and ip in missiles[scene]:
                    missiles[scene][ip]["coords"] = coords
                    nickname = client_nicknames.get(ip, "<неизвестно>")
                    print(f"[move_missile] {ip} → '{nickname}' → {coords} ; {scene}")

            elif command == "explode_missile" and len(parts) >= 3:
                scene, coords = parts[1:3]
                if scene in missiles and ip in missiles[scene]:
                    del missiles[scene][ip]
                    nickname = client_nicknames.get(ip, "<неизвестно>")
                    print(f"[explode_missile] {ip} → '{nickname}' → {coords} ; {scene}")

            elif command == "get_cord" and len(parts) >= 2:
                scene = parts[1].strip()

                last_scene = client_last_scene.get(ip)
                if last_scene != scene:
                    client_last_scene[ip] = scene
                    nickname = client_nicknames.get(ip, "<неизвестно>")
                    print(f"[get_cord] {ip} → '{nickname}' → сцена: {scene}")

                if scene in missiles:
                    for missile_ip, data_dict in missiles[scene].items():
                        name = data_dict["name"]
                        coords = data_dict["coords"]
                        skin = data_dict["skin"]
                        msg = f"missile_info;{name};{scene};{coords};{skin};{missile_ip}"
                        udp_sock.sendto(msg.encode('utf-8'), addr)

        except Exception as e:
            print(f"Ошибка: {e}")

@app.route("/get_cord", methods=["GET"])
def get_cord():
    scene = request.args.get("scene")
    ip = request.remote_addr 

    if scene not in missiles:
        return jsonify([])

    missile_list = []
    for missile_ip, data_dict in missiles[scene].items():
        missile_list.append({
            "name": data_dict["name"],
            "scene": scene,
            "coords": data_dict["coords"],
            "skin": data_dict["skin"],
            "ip": missile_ip
        })
    return jsonify(missile_list)

if __name__ == "__main__":
    threading.Thread(target=main).start()
    context = ('/etc/letsencrypt/live/busiatep.ru/fullchain.pem', 
               '/etc/letsencrypt/live/busiatep.ru/privkey.pem')
    app.run(host='0.0.0.0', port=HTTPS_PORT, ssl_context=context)
