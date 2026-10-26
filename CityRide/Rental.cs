public sealed class Rental
{
    private readonly int _id;
    private readonly string _customerId;
    private readonly string _customerName;
    private readonly string _registration;
    private readonly int _days;
    private readonly decimal _subtotal;
    private readonly decimal _discount;
    private readonly decimal _total;

    public int Id => _id;
    public string CustomerId => _customerId;
    public string CustomerName => _customerName;
    public string Registration => _registration;
    public int Days => _days;
    public decimal Subtotal => _subtotal;
    public decimal Discount => _discount;
    public decimal Total => _total;

    // Readable by everyone, writable only inside this class.
    public RentalStatus Status { get; private set; }

    public Rental(int id, string customerId, string customerName, string registration,
                  int days, decimal subtotal, decimal discount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);
        ArgumentOutOfRangeException.ThrowIfNegative(subtotal);
        if (discount < 0m || discount > subtotal)
            throw new ArgumentOutOfRangeException(nameof(discount), discount,
                "Discount must be between 0 and the subtotal.");

        _id = id;
        _customerId = customerId;
        _customerName = customerName;
        _registration = registration;
        _days = days;
        _subtotal = subtotal;
        _discount = discount;
        _total = subtotal - discount;
        Status = RentalStatus.Active;
    }

    public void Return()
    {
        if (Status == RentalStatus.Returned)
            throw new InvalidOperationException($"Rental {_id} has already been returned.");

        Status = RentalStatus.Returned;
    }

    public override string ToString() =>
        $"#{_id} | {_customerId}/{_customerName} | {_registration} | {_days}d | " +
        $"subtotal {_subtotal:N0} - discount {_discount:N0} = total {_total:N0} EGP | {Status}";
}
