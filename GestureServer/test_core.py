import unittest,json,socket
from gesture_core import GestureGate,packet
class Checks(unittest.TestCase):
    def samples(self,g,label,start,end,score=.95):
        results=[]
        for tick in range(round(start*100),round(end*100)+1,10):
            event=g.update(label,score,tick/100)
            if event:results.append(event)
        return results
    def test_stable(self):
        self.assertEqual(self.samples(GestureGate(),'Open_Palm',0,2),['open_palm'])
    def test_short(self):
        self.assertEqual(self.samples(GestureGate(),'Closed_Fist',0,.3),[])
    def test_low_score(self):
        self.assertEqual(self.samples(GestureGate(),'Open_Palm',0,2,.6),[])
    def test_release_rearms(self):
        g=GestureGate();self.samples(g,'Open_Palm',0,1)
        self.samples(g,'',1.1,2.1)
        self.assertEqual(self.samples(g,'Closed_Fist',2.2,3.2),['fist'])
    def test_switch_requires_release(self):
        g=GestureGate();self.samples(g,'Open_Palm',0,1)
        self.assertEqual(self.samples(g,'Thumb_Up',1.1,3),[])
    def test_gap_not_stable(self):
        g=GestureGate();self.assertIsNone(g.update('Open_Palm',1,0));self.assertIsNone(g.update('Open_Palm',1,2))
    def test_thumb(self):
        self.assertEqual(self.samples(GestureGate(),'Thumb_Up',0,1),['thumb_up'])
    def test_cooldown(self):
        g=GestureGate();self.samples(g,'Open_Palm',0,.6);self.samples(g,'',.7,1.2)
        self.assertEqual(self.samples(g,'Closed_Fist',1.3,2.3),[])
        self.assertEqual(self.samples(g,'Closed_Fist',2.4,3.1),['fist'])
    def test_invalid_packet(self):
        with self.assertRaises(ValueError):packet('s',1,'gesture','invalid',0)
    def test_udp_and_unicode_json(self):
        with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as receiver,socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as sender:
            receiver.bind(('127.0.0.1',0));receiver.settimeout(1)
            raw=packet('session',1,'gesture','open_palm',123)
            sender.sendto(raw,receiver.getsockname())
            message=json.loads(receiver.recvfrom(2048)[0]);self.assertEqual(message['label'],'open_palm');self.assertEqual(message['protocol'],1)
if __name__=='__main__':unittest.main()
