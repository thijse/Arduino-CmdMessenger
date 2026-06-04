export enum WaitState {
  TimeOut = 'TimeOut',
  Normal = 'Normal'
}

type Waiter = {
  resolve: (state: WaitState) => void;
  timeout: ReturnType<typeof setTimeout> | undefined;
};

export class EventWaiter {
  private signaled: boolean;
  private readonly waiters: Waiter[] = [];

  constructor(set = false) {
    this.signaled = set;
  }

  waitOne(timeoutMs: number): Promise<WaitState> {
    if (this.signaled) {
      this.signaled = false;
      return Promise.resolve(WaitState.Normal);
    }

    return new Promise((resolve) => {
      const waiter: Waiter = {
        resolve,
        timeout:
          timeoutMs >= 0
            ? setTimeout(() => {
                this.removeWaiter(waiter);
                resolve(WaitState.TimeOut);
              }, timeoutMs)
            : undefined
      };
      this.waiters.push(waiter);
    });
  }

  set(): void {
    const waiter = this.waiters.shift();
    if (waiter === undefined) {
      this.signaled = true;
      return;
    }
    if (waiter.timeout !== undefined) {
      clearTimeout(waiter.timeout);
    }
    waiter.resolve(WaitState.Normal);
  }

  reset(): void {
    this.signaled = false;
  }

  private removeWaiter(waiter: Waiter): void {
    const index = this.waiters.indexOf(waiter);
    if (index >= 0) {
      this.waiters.splice(index, 1);
    }
  }
}
