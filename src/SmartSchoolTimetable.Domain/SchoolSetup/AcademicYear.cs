using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Text;

namespace SmartSchoolTimetable.Domain.SchoolSetup;

/// <summary>A term inside an academic year (owned by the year aggregate).</summary>
public sealed class Term
{
    private Term()
    {
    }

    internal Term(string name, DateOnly startDate, DateOnly endDate)
    {
        Rename(name, startDate, endDate);
    }

    public long Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    internal void Rename(string name, DateOnly startDate, DateOnly endDate)
    {
        Name = ArabicText.Clean(name);
        NormalizedName = ArabicText.Normalize(name);
        StartDate = startDate;
        EndDate = endDate;
    }

    internal bool Overlaps(DateOnly start, DateOnly end) => start <= EndDate && StartDate <= end;
}

/// <summary>
/// Academic year aggregate with its terms. Rules: start before end; terms inside the year, not overlapping,
/// unique names; at most one current term. A single current year is enforced by the application service.
/// </summary>
public sealed class AcademicYear : VersionedEntity
{
    public const int LabelMaxLength = 50;
    public const int TermNameMaxLength = 100;
    public const int MaxLengthInDays = 731;

    private readonly List<Term> _terms = [];

    private AcademicYear()
    {
    }

    public string Label { get; private set; } = string.Empty;
    public string NormalizedLabel { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsCurrent { get; private set; }
    public long? CurrentTermId { get; private set; }
    public IReadOnlyList<Term> Terms => _terms;

    public Term? CurrentTerm => _terms.FirstOrDefault(term => term.Id == CurrentTermId && CurrentTermId is not null);

    public static AcademicYear Create(string? label, DateOnly startDate, DateOnly endDate)
    {
        ValidateYear(label, startDate, endDate);
        var year = new AcademicYear();
        year.ApplyYear(label!, startDate, endDate);
        return year;
    }

    public void Update(string? label, DateOnly startDate, DateOnly endDate)
    {
        ValidateYear(label, startDate, endDate);
        var errors = new DomainErrors();
        foreach (var term in _terms)
        {
            errors.When(term.StartDate < startDate, nameof(StartDate), DomainErrorCode.TermOutsideYear);
            errors.When(term.EndDate > endDate, nameof(EndDate), DomainErrorCode.TermOutsideYear);
        }
        errors.ThrowIfAny();
        ApplyYear(label!, startDate, endDate);
        Touch();
    }

    public Term AddTerm(string? name, DateOnly startDate, DateOnly endDate)
    {
        ValidateTerm(null, name, startDate, endDate);
        var term = new Term(name!, startDate, endDate);
        _terms.Add(term);
        Touch();
        return term;
    }

    public void UpdateTerm(long termId, string? name, DateOnly startDate, DateOnly endDate)
    {
        var term = FindTerm(termId);
        ValidateTerm(term, name, startDate, endDate);
        term.Rename(name!, startDate, endDate);
        Touch();
    }

    public void RemoveTerm(long termId)
    {
        var term = FindTerm(termId);
        _terms.Remove(term);
        if (CurrentTermId == termId)
            CurrentTermId = null;
        Touch();
    }

    public void SetCurrentTerm(long termId)
    {
        FindTerm(termId);
        CurrentTermId = termId;
        Touch();
    }

    public void MarkCurrent(bool isCurrent)
    {
        if (IsCurrent == isCurrent)
            return;
        IsCurrent = isCurrent;
        Touch();
    }

    public bool HasTerm(long termId) => _terms.Any(term => term.Id == termId);

    private Term FindTerm(long termId) =>
        _terms.FirstOrDefault(term => term.Id == termId) ?? throw new KeyNotFoundException($"Term {termId} not found.");

    private void ApplyYear(string label, DateOnly startDate, DateOnly endDate)
    {
        Label = ArabicText.Clean(label);
        NormalizedLabel = ArabicText.Normalize(label);
        StartDate = startDate;
        EndDate = endDate;
    }

    private static void ValidateYear(string? label, DateOnly startDate, DateOnly endDate) =>
        new DomainErrors()
            .Text(label, nameof(Label), LabelMaxLength)
            .When(endDate <= startDate, nameof(EndDate), DomainErrorCode.InvalidDateRange)
            .When(endDate.DayNumber - startDate.DayNumber > MaxLengthInDays, nameof(EndDate), DomainErrorCode.OutOfRange)
            .ThrowIfAny();

    private void ValidateTerm(Term? existing, string? name, DateOnly startDate, DateOnly endDate)
    {
        var errors = new DomainErrors().Text(name, "Name", TermNameMaxLength);
        if (endDate <= startDate)
            errors.Add(nameof(EndDate), DomainErrorCode.InvalidDateRange);
        else
        {
            errors.When(startDate < StartDate, nameof(StartDate), DomainErrorCode.TermOutsideYear);
            errors.When(endDate > EndDate, nameof(EndDate), DomainErrorCode.TermOutsideYear);
            errors.When(
                _terms.Any(term => term != existing && term.Overlaps(startDate, endDate)),
                nameof(StartDate),
                DomainErrorCode.TermsOverlap);
        }
        var normalized = ArabicText.Normalize(name);
        errors.When(
            normalized.Length > 0 && _terms.Any(term => term != existing && term.NormalizedName == normalized),
            "Name",
            DomainErrorCode.Duplicate);
        errors.ThrowIfAny();
    }
}
