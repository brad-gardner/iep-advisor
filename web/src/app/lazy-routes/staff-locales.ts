// Registers every staff/admin English namespace. Imported by all three lazy
// area chunks (staff, district-admin, platform-admin): district and platform
// admins are staff too, and shared staff components (e.g. the educator school
// filter on the compliance board) render across areas.
import '@/features/educator/staff-locales';
import '@/features/meeting-brief/staff-locales';
import '@/features/evaluation/staff-locales';
import '@/features/family-contact/staff-locales';
import '@/features/meetings/staff-locales';
import '@/features/obligations/staff-locales';
import '@/features/document-authoring/staff-locales';
