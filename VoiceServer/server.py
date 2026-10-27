"""Local speech bridge: Unity WAV -> faster-whisper -> original-language text."""
import argparse
import io
import json
import math
import struct
import threading
import wave
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_BYTES = 6 * 1024 * 1024


def validate_wav(body):
    try:
        with wave.open(io.BytesIO(body), 'rb') as wav:
            channels, width, rate, frames = wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()
            if width != 2 or channels not in (1, 2) or not 8000 <= rate <= 48000:
                raise ValueError('Expected PCM16 WAV, mono/stereo, 8-48 kHz.')
            seconds = frames / rate
            if not 0.4 <= seconds <= 15:
                raise ValueError('Please record between 0.4 and 15 seconds.')
            pcm = wav.readframes(frames)
            if len(pcm) != frames * channels * width:
                raise ValueError('Truncated WAV data.')
    except (wave.Error, EOFError) as exc:
        raise ValueError('Invalid WAV file.') from exc
    count = len(pcm) // 2
    energy = sum(v[0] ** 2 for v in struct.iter_unpack('<h', pcm)) / max(1, count)
    return seconds, math.sqrt(energy) / 32768


def make_handler(model, model_name):
    lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        def reply(self, code, result):
            raw = json.dumps(result, ensure_ascii=False).encode('utf-8')
            self.send_response(code)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(raw)))
            self.end_headers()
            try:
                self.wfile.write(raw)
            except (BrokenPipeError, ConnectionResetError):
                pass

        def do_GET(self):
            if self.path == '/health':
                self.reply(200, {'ok': True, 'model': model_name, 'device': 'cpu', 'languages': ['zh', 'ja', 'en']})
            else:
                self.reply(404, {'ok': False, 'error': 'Not found.'})

        def do_POST(self):
            if self.path != '/transcribe':
                self.reply(404, {'ok': False, 'error': 'Not found.'})
                return
            if not lock.acquire(blocking=False):
                self.close_connection = True
                self.reply(409, {'ok': False, 'error': 'Recognition is busy. Please wait.'})
                return
            try:
                self.connection.settimeout(20)
                try:
                    length = int(self.headers.get('Content-Length', '0'))
                except ValueError:
                    raise ValueError('Invalid Content-Length.')
                if not 44 < length <= MAX_BYTES:
                    raise ValueError('WAV request is empty or too large.')
                body = self.rfile.read(length)
                if len(body) != length:
                    raise ValueError('Incomplete request.')
                seconds, rms = validate_wav(body)
                if rms < 0.003:
                    self.reply(200, {'ok': True, 'text': '', 'language': '', 'language_probability': 0.0, 'warning': 'No clear sound detected.'})
                    return
                # language=None は自動言語検出、task=transcribe は原言語を維持。
                segments, info = model.transcribe(
                    io.BytesIO(body), language=None, task='transcribe', beam_size=3,
                    vad_filter=True, condition_on_previous_text=False,
                    vad_parameters={'min_silence_duration_ms': 400},
                )
                text = ''.join(s.text for s in segments if s.no_speech_prob < 0.7).strip()
                language = info.language
                probability = float(info.language_probability)
                warning = ''
                if language not in ('zh', 'ja', 'en'):
                    warning = 'This prototype accepts Chinese, Japanese and English only.'
                    text = ''
                elif probability < 0.35:
                    warning = 'Language uncertain. Please repeat a longer sentence.'
                    text = ''
                elif not text:
                    warning = 'No clear speech detected.'
                self.reply(200, {'ok': True, 'text': text[:200], 'language': language,
                                 'language_probability': probability, 'warning': warning,
                                 'audio_seconds': round(seconds, 2)})
            except ValueError as exc:
                self.close_connection = True
                self.reply(400, {'ok': False, 'error': str(exc)})
            except Exception as exc:
                self.close_connection = True
                print('Recognition error:', repr(exc), flush=True)
                self.reply(500, {'ok': False, 'error': 'Recognition failed. Check the server window.'})
            finally:
                lock.release()
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', default='small', help='Multilingual model; do not use .en models.')
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    from faster_whisper import WhisperModel
    print('Loading model. First run downloads model files; please wait.', flush=True)
    model = WhisperModel(args.model, device='cpu', compute_type='int8')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), make_handler(model, args.model))
    print(f'READY: http://127.0.0.1:{args.port}/health', flush=True)
    print('Keep this window open. Ctrl+C to stop.', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()

if __name__ == '__main__':
    main()
