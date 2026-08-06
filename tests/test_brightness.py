import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from ddcbright.app import clamp_brightness


def test_clamp_brightness():
    assert clamp_brightness(50) == 50
    assert clamp_brightness(0) == 0
    assert clamp_brightness(100) == 100
    assert clamp_brightness(-10) == 0
    assert clamp_brightness(150) == 100


if __name__ == '__main__':
    test_clamp_brightness()
    print("ok")
