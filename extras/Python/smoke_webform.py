"""Smoke test: start WebForm, send a WebSocket message, verify round-trip."""
import sys, os, time, json, threading
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "samples"))

from shared.web_form import WebForm

errors = []

def test_webform():
    form = WebForm(title="Smoke Test", port=18099)

    # Register a command handler
    received = []
    form.on_command("ping", lambda v: received.append(v))

    # Start non-blocking
    form.start(block=False)
    time.sleep(1.0)  # Give server time to bind

    # Connect via websocket
    import websockets.sync.client as wsc
    ws = wsc.connect("ws://localhost:18099/ws")

    # Send a command
    ws.send(json.dumps({"command": "ping", "value": "hello"}))
    time.sleep(0.5)

    if received != ["hello"]:
        errors.append(f"Expected ['hello'], got {received}")

    # Test broadcast back
    form.send_to_clients({"type": "test", "msg": "world"})
    time.sleep(0.3)

    response = ws.recv(timeout=2)
    data = json.loads(response)
    if data != {"type": "test", "msg": "world"}:
        errors.append(f"Expected test msg, got {data}")

    ws.close()
    form.stop()
    time.sleep(0.5)

test_webform()

if errors:
    print("FAILURES:")
    for e in errors:
        print(f"  - {e}")
    sys.exit(1)
else:
    print("ALL WEBFORM SMOKE TESTS PASSED")
