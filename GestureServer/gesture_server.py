"""Camera -> MediaPipe Tasks -> stable gesture -> loopback UDP -> Unity."""
import argparse
import socket
import sys
import time
import uuid
from gesture_core import GestureGate, packet
from download_model import ensure_model

# Landmark connections are drawn directly, avoiding the removed legacy mp.solutions API.
EDGES = [(0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),
         (5,9),(9,10),(10,11),(11,12),(9,13),(13,14),(14,15),(15,16),
         (13,17),(0,17),(17,18),(18,19),(19,20)]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--camera', type=int, default=0)
    parser.add_argument('--port', type=int, default=8766)
    parser.add_argument('--hold', type=float, default=0.5)
    parser.add_argument('--score', type=float, default=0.75)
    parser.add_argument('--no-preview', action='store_true')
    args = parser.parse_args()
    if not 1 <= args.port <= 65535 or args.hold < 0.2 or not 0 <= args.score <= 1:
        parser.error('Invalid port, hold or score')
    import cv2
    import mediapipe as mp
    from mediapipe.tasks import python
    from mediapipe.tasks.python import vision
    model = ensure_model()
    camera = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW if sys.platform == 'win32' else cv2.CAP_ANY)
    if not camera.isOpened():
        camera.release()
        camera = cv2.VideoCapture(args.camera)
    if not camera.isOpened():
        camera.release()
        raise RuntimeError('Cannot open camera. Close Teams/Zoom, check Windows camera permissions or try --camera 1.')
    camera.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    options = vision.GestureRecognizerOptions(
        base_options=python.BaseOptions(model_asset_path=str(model)),
        running_mode=vision.RunningMode.VIDEO, num_hands=1,
        min_hand_detection_confidence=0.6,
        min_hand_presence_confidence=0.6, min_tracking_confidence=0.6)
    gate = GestureGate(hold=args.hold, score=args.score)
    session, seq = uuid.uuid4().hex, 0
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    destination = ('127.0.0.1', args.port)
    heartbeat_at, last_timestamp, failed = 0.0, -1, 0
    pending = []
    print('Hold a gesture for 0.5s; lower your hand before the next gesture. Q/Esc quits preview. Ctrl+C stops.', flush=True)
    try:
        with vision.GestureRecognizer.create_from_options(options) as recognizer:
            print(f'READY: camera {args.camera} -> Unity UDP 127.0.0.1:{args.port}', flush=True)
            while True:
                ok, frame = camera.read()
                now = time.monotonic()
                if not ok:
                    failed += 1
                    if failed >= 20:
                        raise RuntimeError('Camera stopped producing frames. Reconnect camera and restart.')
                    time.sleep(0.05)
                    continue
                failed = 0
                frame = cv2.flip(frame, 1)
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                timestamp = max(last_timestamp + 1, int(now * 1000))
                last_timestamp = timestamp
                result = recognizer.recognize_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb), timestamp)
                label, score = '', 0.0
                if result.gestures and result.gestures[0]:
                    category = max(result.gestures[0], key=lambda c: c.score)
                    label, score = category.category_name, float(category.score)
                emitted = gate.update(label, score, time.monotonic())
                if emitted:
                    seq += 1
                    raw = packet(session, seq, 'gesture', emitted, time.time())
                    sock.sendto(raw, destination)
                    # Two short repeats share the event ID; Unity drops duplicates.
                    pending.extend([(now + 0.08, raw), (now + 0.16, raw)])
                    print(f'GESTURE #{seq}: {emitted}', flush=True)
                for due, raw in pending[:]:
                    if now >= due:
                        sock.sendto(raw, destination)
                        pending.remove((due, raw))
                if now >= heartbeat_at:
                    sock.sendto(packet(session, seq, 'heartbeat', '', time.time()), destination)
                    heartbeat_at = now + 0.5
                if not args.no_preview:
                    h, w = frame.shape[:2]
                    for hand in result.hand_landmarks:
                        points = [(int(p.x * w), int(p.y * h)) for p in hand]
                        for a,b in EDGES:
                            cv2.line(frame, points[a], points[b], (120,255,160), 2)
                        for point in points:
                            cv2.circle(frame, point, 3, (240,240,240), -1)
                    cv2.putText(frame, f'{label or "No hand"} {score:.2f}', (12,28), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (80,255,140), 2)
                    cv2.putText(frame, 'Lower hand to re-arm' if gate.latched else 'Hold steady...', (12,58), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (80,255,140), 2)
                    cv2.imshow('KotoMogu Gesture - Q to quit', frame)
                    if cv2.waitKey(1) & 0xff in (ord('q'), 27):
                        break
                    if cv2.getWindowProperty('KotoMogu Gesture - Q to quit', cv2.WND_PROP_VISIBLE) < 1:
                        break
    finally:
        camera.release()
        sock.close()
        if not args.no_preview:
            cv2.destroyAllWindows()

if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print('Stopped.')
    except Exception as ex:
        print('ERROR:', ex, flush=True)
        raise SystemExit(1)
