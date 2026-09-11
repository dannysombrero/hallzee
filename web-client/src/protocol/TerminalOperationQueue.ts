import { abortCheck } from "../app/errors";
export class TerminalOperationQueue {
  private tail: Promise<unknown> = Promise.resolve();
  run<T>(_kind: string, action: () => Promise<T>, signal?: AbortSignal): Promise<T> {
    const task = this.tail.then(() => {
      abortCheck(signal);
      return action();
    });
    this.tail = task.catch(() => {});
    return task;
  }
}
