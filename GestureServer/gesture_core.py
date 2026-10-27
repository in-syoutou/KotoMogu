"""Gesture stabilization and local message protocol. No camera dependencies."""
import json
import math

LABELS = {'Open_Palm': 'open_palm', 'Closed_Fist': 'fist', 'Thumb_Up': 'thumb_up'}

class GestureGate:
    def __init__(self, hold=0.5, release=0.4, cooldown=2.0, score=0.75):
        self.hold, self.release, self.cooldown, self.score = hold, release, cooldown, score
        self.candidate = None
        self.since = 0.0
        self.frames = 0
        self.latched = False
        self.neutral_since = None
        self.allowed_at = 0.0
        self.last_time = None

    def update(self, raw_label, confidence, now):
        if not math.isfinite(now):
            return None
        if self.last_time is not None and (now < self.last_time or now - self.last_time > 0.35):
            self.candidate, self.frames, self.neutral_since = None, 0, None
        self.last_time = now
        label = LABELS.get(raw_label) if math.isfinite(confidence) and confidence >= self.score else None
        if label is None:
            self.candidate, self.frames = None, 0
            if self.neutral_since is None:
                self.neutral_since = now
            if now - self.neutral_since >= self.release:
                self.latched = False
            return None
        self.neutral_since = None
        if self.latched:
            return None
        if label != self.candidate:
            self.candidate, self.since, self.frames = label, now, 1
            return None
        self.frames += 1
        if now - self.since >= self.hold and self.frames >= 4 and now >= self.allowed_at:
            self.latched = True
            self.allowed_at = now + self.cooldown
            return label
        return None

def packet(session, sequence, kind, label, now):
    if kind not in ('gesture', 'heartbeat'):
        raise ValueError('Unknown message kind')
    if kind == 'gesture' and label not in LABELS.values():
        raise ValueError('Unknown gesture')
    return json.dumps({'protocol': 1, 'session': session, 'seq': sequence,
                       'kind': kind, 'label': label, 'sent_at': now},
                      separators=(',', ':'), allow_nan=False).encode('utf-8')
