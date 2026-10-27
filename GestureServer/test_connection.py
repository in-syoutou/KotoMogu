"""No camera required. Sends a synthetic open-palm event to Unity."""
import socket,time,uuid
from gesture_core import packet
with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as s:
    session=uuid.uuid4().hex
    print('Click Unity Game now. A test Greet will be sent in 5 seconds.',flush=True)
    time.sleep(5)
    raw=packet(session,1,'gesture','open_palm',time.time())
    for _ in range(3):
        s.sendto(raw,('127.0.0.1',8766));time.sleep(.08)
print('Test event sent. Check Unity: the pet should greet only ONCE. No acknowledgement is provided.')
