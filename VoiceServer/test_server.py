"""Transport tests use a fake recognizer; no model download or ASR accuracy test."""
import io
import json
import math
import struct
import threading
import unittest
import urllib.request
import urllib.error
import wave
from http.server import ThreadingHTTPServer
from types import SimpleNamespace
from server import make_handler, validate_wav


def audio(silent=False):
    data = b''.join(struct.pack('<h', 0 if silent else int(5000*math.sin(i*.15))) for i in range(16000))
    out = io.BytesIO()
    with wave.open(out, 'wb') as wav:
        wav.setparams((1, 2, 16000, 0, 'NONE', 'not compressed'))
        wav.writeframes(data)
    return out.getvalue()


class Model:
    lang = 'ja'
    probability = .95
    text = 'ジャンプして'
    calls = 0
    def transcribe(self, source, **kwargs):
        self.calls += 1
        assert kwargs['language'] is None and kwargs['task'] == 'transcribe'
        return [SimpleNamespace(text=self.text, no_speech_prob=.01)], SimpleNamespace(language=self.lang, language_probability=self.probability)


class ServerChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = Model()
        cls.http = ThreadingHTTPServer(('127.0.0.1', 0), make_handler(cls.model, 'fake-test'))
        cls.worker = threading.Thread(target=cls.http.serve_forever, daemon=True)
        cls.worker.start()
        cls.url = 'http://127.0.0.1:' + str(cls.http.server_port)

    @classmethod
    def tearDownClass(cls):
        cls.http.shutdown(); cls.http.server_close(); cls.worker.join()

    def setUp(self):
        self.model.lang = 'ja'; self.model.probability = .95; self.model.text = 'ジャンプして'

    def request(self, path='/transcribe', data=None):
        request = urllib.request.Request(self.url+path, data=data)
        try:
            with urllib.request.urlopen(request, timeout=5) as r: return r.status, json.load(r)
        except urllib.error.HTTPError as r:
            return r.code, json.load(r)

    def test_health(self):
        code, data = self.request('/health')
        self.assertEqual(code, 200); self.assertTrue(data['ok'])

    def test_multilingual_original_text(self):
        for language, text in [('ja','ジャンプして'), ('zh','请跳一下'), ('en','Please jump')]:
            self.model.lang=language; self.model.text=text
            code, result=self.request(data=audio())
            self.assertEqual(code,200); self.assertEqual(result['language'],language); self.assertEqual(result['text'],text)

    def test_silence_skips_model(self):
        calls=self.model.calls
        _, result=self.request(data=audio(True))
        self.assertEqual(result['text'],''); self.assertEqual(self.model.calls,calls)

    def test_bad_wav(self):
        code, _=self.request(data=b'bad'*100)
        self.assertEqual(code,400)

    def test_unsupported_language(self):
        self.model.lang='fr'
        _, result=self.request(data=audio())
        self.assertEqual(result['text'],''); self.assertTrue(result['warning'])

    def test_uncertain_language(self):
        self.model.probability=.2
        _, result=self.request(data=audio())
        self.assertEqual(result['text'],'')

    def test_no_speech(self):
        self.model.text=''
        _, result=self.request(data=audio())
        self.assertEqual(result['text'],''); self.assertTrue(result['warning'])

    def test_unknown_path(self):
        code,_=self.request('/unknown')
        self.assertEqual(code,404)

if __name__ == '__main__':
    unittest.main()
