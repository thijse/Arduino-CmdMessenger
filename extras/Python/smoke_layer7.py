"""Layer 7 smoke test — CommunicationManager + CmdMessenger facade.

Strategy: two CmdMessenger instances connected via a loop:// virtual serial
pipe — but the loop:// transport mirrors what *we* write, so any line we
send arrives back to us. We use that to verify the full parse pipeline,
callbacks, and synchronous-ack handling.
"""
from __future__ import annotations

import threading
import time

import cmd_messenger as cm
from cmd_messenger.transport.serial import SerialSettings, SerialTransport


def make_messenger(port: str = "loop://") -> tuple[cm.CmdMessenger, SerialTransport]:
    settings = SerialSettings(port_name=port, baud_rate=115200, timeout=100)
    transport = SerialTransport(settings)
    messenger = cm.CmdMessenger(transport, board_type=cm.BoardType.BIT_32)
    assert messenger.connect()
    return messenger, transport


def t1_callback_dispatch():
    print("[1] CmdMessenger dispatches received commands to attached callbacks")
    m, _ = make_messenger()
    received: list[cm.ReceivedCommand] = []
    default_hits: list[int] = []

    def on_4(cmd: cm.ReceivedCommand):
        received.append(cmd)

    def on_default(cmd: cm.ReceivedCommand):
        default_hits.append(cmd.cmd_id)

    m.attach(4, on_4)
    m.attach(on_default)

    # Send a non-ack command. Because loop:// echoes, we receive our own line.
    m.send_command(cm.SendCommand(4, "hello", 42))
    m.send_command(cm.SendCommand(7, "unknown"))

    deadline = time.time() + 2.0
    while time.time() < deadline and (not received or not default_hits):
        time.sleep(0.01)

    assert received, "no callback for cmd 4"
    assert received[0].cmd_id == 4
    assert received[0].read_string_arg() == "hello"
    assert received[0].read_int32_arg() == 42
    assert default_hits == [7], default_hits
    m.disconnect()
    m.dispose()
    print(f"   ok (received {[c.cmd_id for c in received]}, default {default_hits})")


def t2_new_line_events():
    print("[2] new_line_sent and new_line_received fire")
    m, _ = make_messenger()
    sent: list[cm.SendCommand] = []
    recv: list[cm.ReceivedCommand] = []
    m.new_line_sent += sent.append
    m.new_line_received += recv.append

    m.send_command(cm.SendCommand(10, "abc"))
    deadline = time.time() + 2.0
    while time.time() < deadline and (not sent or not recv):
        time.sleep(0.01)

    assert sent and sent[0].cmd_id == 10
    assert recv and recv[0].cmd_id == 10
    m.disconnect()
    m.dispose()
    print(f"   ok (sent={[c.cmd_id for c in sent]}, recv={[c.cmd_id for c in recv]})")


def t3_synchronous_ack():
    print("[3] Synchronous send with ack — peer thread answers after the request")
    m, _ = make_messenger()

    # The "peer" runs on a timer thread. When triggered, it writes an
    # acknowledge command straight to the transport (bypassing the send queue
    # — the send queue would deadlock against the suspended receive queue).
    def peer_ack():
        time.sleep(0.05)
        # Write directly to the underlying transport, bypassing the send
        # data lock (which the main thread is holding while it waits).
        m._communication_manager._transport.write(b"99,ackpayload;")

    threading.Thread(target=peer_ack, daemon=True).start()

    req = cm.SendCommand(20, "ping", ack_cmd_id=99, timeout=1500)
    result = m.send_command(req)

    assert result is not None
    assert result.ok, f"no ack received: cmd_id={result.cmd_id}"
    assert result.cmd_id == 99
    assert result.read_string_arg() == "ackpayload"
    m.disconnect()
    m.dispose()
    print("   ok")


def t4_print_lf_cr():
    print("[4] print_lf_cr appends \\r\\n and parser strips it")
    m, _ = make_messenger()
    m.print_lf_cr = True

    received: list[cm.ReceivedCommand] = []
    m.attach(33, received.append)
    m.send_command(cm.SendCommand(33, "lfcr"))
    deadline = time.time() + 1.5
    while time.time() < deadline and not received:
        time.sleep(0.01)
    assert received and received[0].read_string_arg() == "lfcr"
    m.disconnect()
    m.dispose()
    print("   ok")


def t5_escape_handling():
    print("[5] Binary strings with separators round-trip via escape handling")
    m, _ = make_messenger()
    got: list[cm.ReceivedCommand] = []
    m.attach(50, got.append)
    # 'a,b;c/d' contains all three special characters. add_bin_argument(str)
    # is the documented way to send arbitrary strings — it escapes on send and
    # the receiver unescapes via read_bin_string_arg.
    payload = "a,b;c/d"
    sc = cm.SendCommand(50)
    sc.add_bin_argument(payload)
    m.send_command(sc)
    deadline = time.time() + 1.5
    while time.time() < deadline and not got:
        time.sleep(0.01)
    assert got, "command not received"
    decoded = got[0].read_bin_string_arg()
    assert decoded == payload, decoded
    m.disconnect()
    m.dispose()
    print("   ok")


if __name__ == "__main__":
    t1_callback_dispatch()
    t2_new_line_events()
    t3_synchronous_ack()
    t4_print_lf_cr()
    t5_escape_handling()
    print("\nAll Layer 7 smoke tests passed.")
