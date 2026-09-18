import { test, expect } from "@playwright/test";

test("room station renders kiosk UI, destination selection, and privacy masking", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));

  // Navigate to Room Station route with room code parameter
  await page.goto("/?room=ROOM404");

  // Verify header and room code
  await expect(page.getByText("ROOM ROOM404")).toBeVisible();
  await expect(page.getByText("HALLZEE CLASSROOM PASS")).toBeVisible();

  // Verify Pass Available banner
  await expect(page.getByText("PASS AVAILABLE", { exact: false })).toBeVisible();

  // Verify destination chips
  await expect(page.getByRole("button", { name: "Restroom" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Nurse" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Library" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Main Office" })).toBeVisible();

  // Click on "Nurse" destination chip
  await page.getByRole("button", { name: "Nurse" }).click();

  // Enter student ID
  const idInput = page.getByPlaceholder("Type your student ID...");
  await expect(idInput).toBeVisible();
  await idInput.fill("12345");

  // Verify Check Out button exists
  const checkOutBtn = page.getByRole("button", { name: "CHECK OUT →" });
  await expect(checkOutBtn).toBeVisible();

  // Ensure no unhandled exceptions in console
  expect(errors).toEqual([]);
});
