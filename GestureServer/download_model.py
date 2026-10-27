from pathlib import Path
import urllib.request
import zipfile

URL = 'https://storage.googleapis.com/mediapipe-models/gesture_recognizer/gesture_recognizer/float16/latest/gesture_recognizer.task'
MODEL = Path(__file__).resolve().parent / 'models' / 'gesture_recognizer.task'

def ensure_model():
    if MODEL.is_file() and zipfile.is_zipfile(MODEL):
        return MODEL
    MODEL.parent.mkdir(parents=True, exist_ok=True)
    temporary = MODEL.with_suffix('.download')
    print('Downloading gesture model from Google (first run only)...', flush=True)
    try:
        with urllib.request.urlopen(URL, timeout=60) as response, temporary.open('wb') as f:
            total = 0
            while True:
                block = response.read(256 * 1024)
                if not block:
                    break
                f.write(block)
                total += len(block)
                if total > 100 * 1024 * 1024:
                    raise ValueError('Unexpected model size')
        if not zipfile.is_zipfile(temporary):
            raise ValueError('Downloaded file is not a MediaPipe model bundle')
        temporary.replace(MODEL)
    finally:
        temporary.unlink(missing_ok=True)
    return MODEL

if __name__ == '__main__':
    try:
        print('Model ready:', ensure_model())
    except Exception as ex:
        print('Model download failed:', ex)
        print('Retry 01_setup.bat, or download the URL in README into models/gesture_recognizer.task.')
        raise SystemExit(1)
