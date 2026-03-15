import { CalendarDate, getLocalTimeZone, today } from "@internationalized/date";
import { z } from "zod";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { UserRole } from "@/enums/UserRole";

const MAX_PAGE_SIZE = 100;

const dateValueSchema = z.instanceof(CalendarDate);

export const createUserSchema = z
  .object({
    confirmPassword: z.string(),
    email: z.email("Invalid email address"),
    password: z.string().min(8, "Must be at least 8 characters"),
    role: z.enum(UserRole).default(UserRole.User),
    userName: z
      .string()
      .min(2, "Must be at least 2 characters")
      .max(50, "Max 50 characters")
      .regex(/^[a-zA-Z0-9_.-]+$/, "Only letters, numbers, underscores, dots, and hyphens"),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords don't match",
    path: ["confirmPassword"],
  });

export type CreateUserSchema = z.output<typeof createUserSchema>;

type UserDateField =
  | "createdAfter"
  | "createdBefore"
  | "updatedAfter"
  | "updatedBefore"
  | "deletedAfter"
  | "deletedBefore"
  | "lockedOutAfter"
  | "lockedOutBefore";

// Query / Search Schema (UI)

export const userQueryUiSchema = z
  .object({
    page: z.number().int().min(0).default(0),
    pageSize: z.number().int().min(1).max(MAX_PAGE_SIZE).default(20),
    sortBy: z.enum(SortBy).default(SortBy.CreatedAt),
    sortDirection: z.enum(SortDirection).default(SortDirection.Desc),

    userName: z.string().max(50).nullish(),
    userEmail: z.string().email().nullish(),
    role: z.nativeEnum(UserRole).nullish(),
    isLockedOut: z.boolean().nullish(),

    showDeleted: z.boolean().default(false),
    showDeletedOnly: z.boolean().default(false),

    // Date filters (UI types — CalendarDate)
    createdAfter: dateValueSchema.nullish(),
    createdBefore: dateValueSchema.nullish(),
    updatedAfter: dateValueSchema.nullish(),
    updatedBefore: dateValueSchema.nullish(),
    deletedAfter: dateValueSchema.nullish(),
    deletedBefore: dateValueSchema.nullish(),
    lockedOutAfter: dateValueSchema.nullish(),
    lockedOutBefore: dateValueSchema.nullish(),
  })
  // Date range consistency
  .refine(
    (d) => !d.createdAfter || !d.createdBefore || d.createdAfter.compare(d.createdBefore) < 0,
    {
      message: "CreatedAfter must be earlier than CreatedBefore",
      path: ["createdAfter"],
    },
  )
  .refine(
    (d) => !d.updatedAfter || !d.updatedBefore || d.updatedAfter.compare(d.updatedBefore) < 0,
    {
      message: "UpdatedAfter must be earlier than UpdatedBefore",
      path: ["updatedAfter"],
    },
  )
  .refine(
    (d) => !d.deletedAfter || !d.deletedBefore || d.deletedAfter.compare(d.deletedBefore) < 0,
    {
      message: "DeletedAfter must be earlier than DeletedBefore",
      path: ["deletedAfter"],
    },
  )
  .refine(
    (d) =>
      !d.lockedOutAfter || !d.lockedOutBefore || d.lockedOutAfter.compare(d.lockedOutBefore) < 0,
    {
      message: "LockedOutAfter must be earlier than LockedOutBefore",
      path: ["lockedOutAfter"],
    },
  )
  // ShowDeleted + showDeletedOnly mutual exclusivity
  .refine((d) => !(d.showDeleted && d.showDeletedOnly), {
    message: "ShowDeleted and ShowDeletedOnly cannot both be true",
    path: ["showDeletedOnly"],
  })
  // No future dates
  .superRefine((data, ctx) => {
    const now = today(getLocalTimeZone());
    const dateFields: UserDateField[] = [
      "createdAfter",
      "createdBefore",
      "updatedAfter",
      "updatedBefore",
      "deletedAfter",
      "deletedBefore",
      "lockedOutAfter",
      "lockedOutBefore",
    ];

    for (const field of dateFields) {
      const value = data[field];
      if (value && value.compare(now) > 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "Date cannot be in the future",
          path: [field],
        });
      }
    }
  });

// Query / Search Schema (API — serialized dates)

export const userQueryApiSchema = userQueryUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  deletedAfter: v.deletedAfter?.toString() ?? null,
  deletedBefore: v.deletedBefore?.toString() ?? null,
  lockedOutAfter: v.lockedOutAfter?.toString() ?? null,
  lockedOutBefore: v.lockedOutBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
}));

// Update User Schema

export const updateUserSchema = z.object({
  email: z
    .string()
    .email("A valid email address is required")
    .max(256, "Email cannot exceed 256 characters")
    .nullish(),
  role: z.nativeEnum(UserRole).nullish(),
  userName: z
    .string()
    .max(50, "Username cannot exceed 50 characters")
    .regex(
      /^[a-zA-Z0-9_.-]*$/,
      "Username can only contain letters, numbers, underscores, dots and hyphens",
    )
    .nullish(),
});

// Restrict User Schema

export const restrictUserSchema = z.object({
  lockoutEndDate: z
    .instanceof(CalendarDate)
    .refine(
      (d) => d.compare(today(getLocalTimeZone())) > 0,
      "Lockout end date must be in the future",
    ),
  userId: z.uuid("User ID is required"),
});

// Delete Users Schema

export const deleteUsersSchema = z.object({
  userIds: z
    .array(z.uuid("User IDs cannot be empty GUIDs"))
    .min(1, "At least one user ID is required")
    .max(100, "Cannot delete more than 100 users in a single request")
    .refine((ids) => new Set(ids).size === ids.length, "Duplicate user IDs are not allowed"),
});

export const changePasswordSchema = z
  .object({
    confirmPassword: z.string(),
    initialPassword: z.string().nonempty("Password is required"),
    newPassword: z
      .string()
      .min(8, "Password must be at least 8 characters")
      .max(100, "Password must not exceed 100 characters")
      .regex(/[A-Z]/, "Password must contain at least one uppercase letter")
      .regex(/[a-z]/, "Password must contain at least one lowercase letter")
      .regex(/[0-9]/, "Password must contain at least one number")
      .regex(/[^a-zA-Z0-9]/, "Password must contain at least one special character"),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

// Inferred Types

export type UserQueryUiState = z.infer<typeof userQueryUiSchema>;
export type UserQueryApiState = z.infer<typeof userQueryApiSchema>;
export type UpdateUserSchema = z.infer<typeof updateUserSchema>;
export type RestrictUserSchema = z.infer<typeof restrictUserSchema>;
export type DeleteUsersSchema = z.infer<typeof deleteUsersSchema>;
export type ChangePasswordSchema = z.infer<typeof changePasswordSchema>;
