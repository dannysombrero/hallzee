const pad = (n: number) => String(n).padStart(2, "0");
export const localDate = (date = new Date()) =>
  `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
export const localTime = (date = new Date()) =>
  `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
export const wallEpoch = (date = new Date()) =>
  Date.UTC(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
    date.getHours(),
    date.getMinutes(),
    date.getSeconds(),
  ) / 1000;
export const elapsed = (epoch: number, date = new Date()) =>
  Math.max(0, Math.floor(wallEpoch(date) - epoch));
export const epochLabel = (epoch: number) => new Date(epoch * 1000).toISOString().slice(11, 19);
export const durationLabel = (seconds: number) =>
  `${Math.floor(seconds / 60)}:${pad(seconds % 60)}`;
