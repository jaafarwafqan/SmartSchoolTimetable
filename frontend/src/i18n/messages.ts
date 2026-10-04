// Arabic UI dictionary, split by area. Numbers are never written into strings: callers pass values already
// formatted by lib/format.ts so the numeral system can follow the school setting.
import { app } from "./ar/app";
import { errors } from "./ar/errors";
import { fields } from "./ar/fields";
import { school } from "./ar/school";

export const ar = { app, errors, fields, school } as const;

export type ApiErrorCode = keyof typeof ar.errors;
export type FieldName = keyof typeof ar.fields;
export type MessageDictionary = typeof ar;
export type Locale = "ar" | "en";

export const activeLocale: Locale = "ar";
export const messages = ar;
