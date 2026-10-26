public sealed class Car : Vehicle
{
    private readonly decimal _oneTimeFee;

    public decimal OneTimeFee => _oneTimeFee;

    public Car(string registration, string model, decimal dailyRate, decimal oneTimeFee)
        : base(registration, model, dailyRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(oneTimeFee);
        _oneTimeFee = oneTimeFee;
    }

    protected override decimal CalculateCostCore(int days) => days * DailyRate + _oneTimeFee;

    public override string GetDescription() => $"Car | {base.GetDescription()}";

    // Hides (does not override) the base method: chosen by the compile-time type.
    public new string GetCategoryLabel() => "Car";
}
