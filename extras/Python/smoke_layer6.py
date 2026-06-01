"""Layer 6 smoke test — queues + strategies."""
from __future__ import annotations

import threading
import time

import cmd_messenger as cm
from cmd_messenger.queue import (
    CollapseCommandStrategy,
    CommandStrategy,
    ListQueue,
    ReceiveCommandQueue,
    SendCommandQueue,
    StaleGeneralStrategy,
    TopCommandStrategy,
)


# ----------------------------------------------------------------------
# Fake CommunicationManager
# ----------------------------------------------------------------------
class FakeCM:
    field_separator = ","
    command_separator = ";"
    escape_character = "/"
    print_lf_cr = False
    board_type = cm.BoardType.BIT_32

    def __init__(self) -> None:
        self.sent_strings: list[str] = []
        self.sent_commands: list[cm.SendCommand] = []
        self.lock = threading.Lock()

    def execute_send_string(self, send_string: str, send_queue_state):
        with self.lock:
            self.sent_strings.append(send_string)

    def execute_send_command(self, send_command, send_queue_state):
        with self.lock:
            self.sent_commands.append(send_command)


def t1_list_queue():
    print("[1] ListQueue basics")
    q: ListQueue[int] = ListQueue()
    q.enqueue(1)
    q.enqueue(2)
    q.enqueue_front(3)  # faithful C# bug: appends at back
    assert list(q) == [1, 2, 3], list(q)
    assert q.peek() == 1
    assert q.dequeue() == 1
    assert list(q) == [2, 3]
    print("   ok")


def t2_collapse_strategy():
    print("[2] CollapseCommandStrategy de-duplicates")
    q: ListQueue[CommandStrategy] = ListQueue()
    a = CollapseCommandStrategy(cm.SendCommand(5, "a"))
    a.command_queue = q
    a.enqueue()
    b = CollapseCommandStrategy(cm.SendCommand(5, "b"))
    b.command_queue = q
    b.enqueue()
    assert len(q) == 1 and q[0] is b
    c = CollapseCommandStrategy(cm.SendCommand(6, "c"))
    c.command_queue = q
    c.enqueue()
    assert len(q) == 2
    print("   ok")


def t3_stale_strategy():
    print("[3] StaleGeneralStrategy drops old commands")
    q: ListQueue[CommandStrategy] = ListQueue()
    sgs = StaleGeneralStrategy(50)
    sgs.command_queue = q
    s1 = CommandStrategy(cm.SendCommand(1))
    s2 = CommandStrategy(cm.SendCommand(2))
    s3 = CommandStrategy(cm.SendCommand(3))
    s1.command.time_stamp = cm.time_utils.millis() - 1000
    s2.command.time_stamp = cm.time_utils.millis() - 500
    s3.command.time_stamp = cm.time_utils.millis()
    s1.command_queue = s2.command_queue = s3.command_queue = q
    s1.enqueue(); s2.enqueue(); s3.enqueue()
    sgs.on_dequeue()
    assert len(q) == 1, [s.command.cmd_id for s in q]
    assert q[0].command.cmd_id == 3
    print("   ok")


def t4_send_queue_pack():
    print("[4] SendCommandQueue packs multiple commands into one buffer")
    fake = FakeCM()
    sq = SendCommandQueue(fake, send_buffer_max_length=200)
    sq.start()
    sq.queue_send_command(cm.SendCommand(1, "a"))
    sq.queue_send_command(cm.SendCommand(2, "b"))
    sq.queue_send_command(cm.SendCommand(3, "c"))
    deadline = time.time() + 1.0
    while time.time() < deadline and not fake.sent_strings:
        time.sleep(0.01)
    sq.stop()
    assert fake.sent_strings, "no string sent"
    combined = "".join(fake.sent_strings)
    assert "1,a;" in combined and "2,b;" in combined and "3,c;" in combined, combined
    print(f"   ok ({fake.sent_strings!r})")


def t5_send_queue_ack_isolated():
    print("[5] SendCommandQueue sends ack-required commands individually")
    fake = FakeCM()
    sq = SendCommandQueue(fake, send_buffer_max_length=200)
    sq.start()
    sq.queue_send_command(cm.SendCommand(1, "first"))
    sq.queue_send_command(cm.SendCommand(2, "needack", ack_cmd_id=99))
    sq.queue_send_command(cm.SendCommand(3, "after"))
    deadline = time.time() + 1.0
    while time.time() < deadline and (
        not fake.sent_commands or not fake.sent_strings
    ):
        time.sleep(0.01)
    sq.stop()
    assert len(fake.sent_commands) == 1, fake.sent_commands
    assert fake.sent_commands[0].cmd_id == 2
    assert any("1,first;" in s for s in fake.sent_strings)
    assert any("3,after;" in s for s in fake.sent_strings)
    print(f"   ok strings={fake.sent_strings!r} acks={[c.cmd_id for c in fake.sent_commands]}")


def t6_receive_queue_dispatch():
    print("[6] ReceiveCommandQueue dispatches via callback on worker thread")
    received: list[cm.ReceivedCommand] = []
    handler_thread: list[str] = []

    def handler(rc):
        received.append(rc)
        handler_thread.append(threading.current_thread().name)

    rq = ReceiveCommandQueue(handler)
    rq.start()
    rq.queue_received_command(cm.ReceivedCommand(["10", "x"]))
    rq.queue_received_command(cm.ReceivedCommand(["11", "y"]))
    deadline = time.time() + 1.0
    while time.time() < deadline and len(received) < 2:
        time.sleep(0.01)
    rq.stop()
    assert len(received) == 2
    assert received[0].cmd_id == 10 and received[1].cmd_id == 11
    assert handler_thread[0] != "MainThread"
    print(f"   ok (worker={handler_thread[0]!r})")


def t7_receive_queue_suspend_signal():
    print("[7] ReceiveCommandQueue suspend → ack consumed by wait_for_cmd")
    received: list[cm.ReceivedCommand] = []
    rq = ReceiveCommandQueue(received.append)
    rq.start()
    rq.suspend()
    rq.prepare_for_cmd(42, cm.SendQueue.DEFAULT)

    def deliver():
        time.sleep(0.05)
        rq.queue_received_command(cm.ReceivedCommand(["42", "ackpayload"]))

    threading.Thread(target=deliver, daemon=True).start()
    ack = rq.wait_for_cmd(timeout_ms=1000)
    rq.resume()
    rq.stop()
    assert ack is not None and ack.cmd_id == 42, ack
    assert ack.read_string_arg() == "ackpayload"
    assert received == [], "matched command should NOT be queued for handler"
    print("   ok")


if __name__ == "__main__":
    t1_list_queue()
    t2_collapse_strategy()
    t3_stale_strategy()
    t4_send_queue_pack()
    t5_send_queue_ack_isolated()
    t6_receive_queue_dispatch()
    t7_receive_queue_suspend_signal()
    print("\nAll Layer 6 smoke tests passed.")
