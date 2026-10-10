import { CalendarCheck, Landmark, School, Star, type LucideIcon } from "lucide-react";
import type { CalendarKind } from "./calendarApi";

/** MF8: one icon per calendar kind (shown with the kind's colour and start border, so colour is never the only cue). */
export const kindIcons: Record<CalendarKind, LucideIcon> = {
  officialHoliday: Landmark,
  schoolHoliday: School,
  exam: CalendarCheck,
  specialDay: Star,
};
