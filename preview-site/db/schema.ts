import { sqliteTable, text } from 'drizzle-orm/sqlite-core';
export const waitlist = sqliteTable('waitlist', {
  email: text('email').primaryKey(),
  joinedAt: text('joined_at').notNull(),
  noticeVersion: text('notice_version'),
});
