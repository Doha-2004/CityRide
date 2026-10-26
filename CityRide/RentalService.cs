public sealed class RentalService
{
    private readonly List<Vehicle> _vehicles = new();
    private readonly List<Rental> _rentals = new();

    // Shared by every RentalService instance in the process.
    public static int SuccessfulRentalCount { get; private set; }

    // AsReadOnly() returns a real wrapper, so it cannot be cast back to List<T>.
    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();
    public IReadOnlyList<Rental> Rentals => _rentals.AsReadOnly();

    public void AddVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (FindVehicle(vehicle.Registration) is not null)
            throw new InvalidOperationException(
                $"A vehicle with registration '{vehicle.Registration}' is already registered.");

        _vehicles.Add(vehicle);
    }

    public decimal Quote(IPriceable item, int days)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.CalculateCost(days);
    }

    public Rental Rent(Customer customer, string registration, int days)
        => Rent(customer, registration, days, 0m);

    public Rental Rent(Customer customer, string registration, int days, decimal discount)
    {
        // Validate first: nothing below this block touches stored state.
        ArgumentNullException.ThrowIfNull(customer);
        Vehicle vehicle = GetAvailableVehicle(registration);
        decimal subtotal = Quote(vehicle, days);
        EnsureValidDiscount(discount, subtotal);

        var rental = new Rental(NextRentalId(), customer.Id, customer.Name,
                                vehicle.Registration, days, subtotal, discount);

        // Commit last: store the rental, then bump the counter.
        _rentals.Add(rental);
        SuccessfulRentalCount++;
        return rental;
    }

    public void Return(int rentalId)
    {
        Rental rental = _rentals.FirstOrDefault(r => r.Id == rentalId)
            ?? throw new InvalidOperationException($"No rental exists with ID {rentalId}.");

        rental.Return();
    }

    // Availability is derived from Active rentals only; there is no separate flag to drift.
    public bool IsAvailable(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        return !_rentals.Any(r => r.Status == RentalStatus.Active
                                  && IsSameRegistration(r.Registration, vehicle.Registration));
    }

    private Vehicle GetAvailableVehicle(string registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        Vehicle vehicle = FindVehicle(registration)
            ?? throw new InvalidOperationException(
                $"No vehicle is registered with registration '{registration.Trim()}'.");

        if (!IsAvailable(vehicle))
            throw new InvalidOperationException(
                $"Vehicle '{vehicle.Registration}' is not available: it has an Active rental.");

        return vehicle;
    }

    private static void EnsureValidDiscount(decimal discount, decimal subtotal)
    {
        if (discount < 0m || discount > subtotal)
            throw new ArgumentOutOfRangeException(nameof(discount), discount,
                $"Discount must be between 0 and the subtotal ({subtotal}).");
    }

    // The ID is the next number in the successful sequence (rejections never consume one).
    private static int NextRentalId() => SuccessfulRentalCount + 1;

    // The single place that decides how registrations are matched (trim + ignore case).
    private Vehicle? FindVehicle(string registration)
    {
        string key = registration.Trim();
        return _vehicles.FirstOrDefault(v => IsSameRegistration(v.Registration, key));
    }

    private static bool IsSameRegistration(string first, string second)
        => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
