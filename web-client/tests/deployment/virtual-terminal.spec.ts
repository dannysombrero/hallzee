import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

test("public code entry, sharing, direct links, checkout, refresh and check-in", async ({ page, browser }) => {
  const code = `VERIFY-${randomUUID().slice(0, 8)}`.toUpperCase();
  const direct = `https://pass.hallzee.com/${code.toLowerCase()}`;
  const studentContext = await browser.newContext();
  const student = await studentContext.newPage();
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  student.on("pageerror", error => errors.push(error.message));
  try {
    await page.goto("/");
    const header = page.locator(".top-window-bar");
    await expect(header.getByRole("button")).toHaveCount(2);
    await expect(header).not.toContainText(/Desktop client|Teacher workspace|Ready offline/);
    await header.getByRole("button", { name: "Start Virtual Terminal", exact: true }).click();
    await page.getByLabel("Terminal code", { exact: true }).fill(code);
    await page.getByRole("dialog").getByRole("button", { name: "Start Virtual Terminal", exact: true }).click();
    await expect(page.getByRole("dialog").getByText("Virtual terminal open", { exact: true })).toBeVisible();
    await expect(page.locator(".room-direct-link")).toHaveText(direct);
    await expect(page.getByRole("img", { name: "QR code to join this terminal" })).toBeVisible();
    await page.screenshot({ path: "test-results/deployment-teacher-sharing.png", fullPage: true });
    await page.getByRole("button", { name: "Done", exact: true }).click();
    await header.getByRole("button", { name: /Virtual Terminal Open/ }).click();
    await expect(page.locator(".room-code")).toHaveText(code);
    await page.getByRole("button", { name: "Done", exact: true }).click();

    await student.goto("https://pass.hallzee.com/");
    await expect(student.getByRole("button", { name: "Join Terminal" })).toBeVisible();
    await student.screenshot({ path: "test-results/deployment-student-join.png", fullPage: true });
    await student.getByLabel("Terminal code", { exact: true }).fill(code.toLowerCase());
    await student.getByRole("button", { name: "Join Terminal" }).click();
    await expect(student).toHaveURL(direct);
    await student.reload();
    await expect(student.getByRole("heading", { name: code, exact: true })).toBeVisible();
    await student.getByLabel("Student ID", { exact: true }).fill("90000001");
    await student.getByRole("button", { name: "CHECK OUT →", exact: true }).click();
    await expect(student.getByText("Have a good trip! Your pass is active.")).toBeVisible();
    await expect(page.getByRole("heading", { name: "1 student out" })).toBeVisible();
    await page.reload();
    await expect(page.getByRole("heading", { name: "1 student out" })).toBeVisible();
    await student.getByRole("button", { name: "Check in", exact: true }).click();
    await student.getByLabel("Student ID", { exact: true }).fill("90000001");
    await student.getByRole("button", { name: "✓ I'M BACK (CHECK IN)", exact: true }).click();
    await expect(student.getByText("Welcome back! You are checked in.")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Pass available", exact: true })).toBeVisible();
    await header.getByRole("button", { name: /Virtual Terminal Open/ }).click();
    page.once("dialog", confirmation => confirmation.accept());
    await page.getByRole("button", { name: "End Session", exact: true }).click();
    await expect(student.getByText("TERMINAL CLOSED", { exact: true }).first()).toBeVisible();
    expect(errors).toEqual([]);
  } finally {
    await studentContext.close();
  }
});
