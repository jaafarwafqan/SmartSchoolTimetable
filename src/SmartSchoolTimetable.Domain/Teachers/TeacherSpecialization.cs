namespace SmartSchoolTimetable.Domain.Teachers;

/// <summary>A subject the teacher is qualified to teach (owned by <see cref="Teacher"/>).</summary>
public sealed record TeacherSpecialization(long SubjectId);
