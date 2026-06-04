/** How a send command is added to the send queue. */
export enum SendQueue {
  Default = 0,
  InFrontQueue = 1,
  AtEndQueue = 2,
  WaitForEmptyQueue = 3,
  ClearQueue = 4
}

/** How receive-queue behavior is controlled before sending. */
export enum ReceiveQueue {
  Default = 0,
  WaitForEmptyQueue = 1,
  ClearQueue = 2
}

/** Whether a send should use the async queue or bypass it. */
export enum UseQueue {
  UseQueue = 0,
  BypassQueue = 1
}

/** Target Arduino word width. On AVR-style boards, double is float32. */
export enum BoardType {
  Bit16 = 0,
  Bit32 = 1
}
