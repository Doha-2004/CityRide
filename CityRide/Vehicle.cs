public abstract class Vehicle : IPriceable
{
    private readonly string _registration;
    private readonly string _model;
    private readonly decimal _dailyRate;

    public string Registration => _registration;
    public string Model => _model;
    public decimal DailyRate => _dailyRate;

    protected Vehicle(string registration, string model, decimal dailyRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dailyRate);

        _registration = registration.Trim();
        _model = model.Trim();
        _dailyRate = dailyRate;
    }

    // Template Method: the validation lives here once; subclasses only supply the calculation.
    public decimal CalculateCost(int days)
    {
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be positive.");

        return CalculateCostCore(days);
    }

    protected abstract decimal CalculateCostCore(int days);

    public virtual string GetDescription() => $"{Registration}: {Model}";

    // Deliberately non-virtual: used only by the new-vs-override experiment.
    public string GetCategoryLabel() => "Vehicle";
}
