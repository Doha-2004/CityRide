public sealed class Motorcycle : Vehicle
{
    public Motorcycle(string registration, string model, decimal dailyRate)
        : base(registration, model, dailyRate)
    {
    }

    protected override decimal CalculateCostCore(int days) => days * DailyRate;

    public override string GetDescription() => $"Motorcycle | {base.GetDescription()}";
}
