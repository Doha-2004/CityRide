using System.Globalization;

public static class Program
{
    private const string CarRegistration = "CAR-01";
    private const string BikeRegistration = "BIKE-01";

    private static int _failureCount;

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        if (args.Contains("--check"))
            RunChecks();
        else
            RunDemo();

        return _failureCount == 0 ? 0 : 1;
    }

    // Scenario shared by the demo and the checks: one service, Amina, one car, one motorcycle.
    private static (RentalService Service, Customer Customer, Car Car, Motorcycle Bike) CreateScenario()
    {
        var service = new RentalService();
        var customer = new Customer("C1", "Amina");
        var car = new Car(CarRegistration, "Toyota City", dailyRate: 500m, oneTimeFee: 100m);
        var bike = new Motorcycle(BikeRegistration, "Honda Street", dailyRate: 200m);

        service.AddVehicle(car);
        service.AddVehicle(bike);
        return (service, customer, car, bike);
    }

    // ================================================================== DEMO

    private static void RunDemo()
    {
        var (service, amina, car, bike) = CreateScenario();

        Section("1-2. Quotes (counter must stay 0)");
        Console.WriteLine($"Quote car 3 days  : {service.Quote(car, 3):N0}  (expected 1,600)");
        Console.WriteLine($"Quote bike 2 days : {service.Quote(bike, 2):N0}  (expected 400)");
        PrintCounter();

        Section("3. Rent car 3 days, discount 100 (4-arg overload)");
        Rental carRental = service.Rent(amina, CarRegistration, 3, 100m);
        Console.WriteLine(carRental);
        PrintCounter();

        Section("4. Rent bike 2 days (3-arg overload)");
        Console.WriteLine(service.Rent(amina, BikeRegistration, 2));
        PrintCounter("expected 2");

        Section("5. Rejections while the car is Active");
        Reject(service, "Rent car again while Active", () => service.Rent(amina, CarRegistration, 1));
        Reject(service, "Rent unknown registration", () => service.Rent(amina, "NOPE-99", 1));
        Reject(service, "Add duplicate ' car-01 '", () => service.AddVehicle(new Car(" car-01 ", "Duplicate", 300m, 0m)));
        Reject(service, "Return unknown rental ID 999", () => service.Return(999));

        Section("6. Return the first rental");
        service.Return(carRental.Id);
        Console.WriteLine($"Rental #{carRental.Id} is {carRental.Status}; car available: {service.IsAvailable(car)}");
        PrintCounter("still 2");
        Reject(service, "Return the same rental again", () => service.Return(carRental.Id));

        Section("7. Rejections while the car is available");
        Reject(service, "Rent for 0 days", () => service.Rent(amina, CarRegistration, 0));
        Reject(service, "Rent for -2 days", () => service.Rent(amina, CarRegistration, -2));
        Reject(service, "Rent with discount -5", () => service.Rent(amina, CarRegistration, 1, -5m));
        Reject(service, "Rent with discount 700 (quote is 600)", () => service.Rent(amina, CarRegistration, 1, 700m));
        Console.WriteLine($"Car still available: {service.IsAvailable(car)}");
        PrintCounter("still 2");

        Section("8. Rent car 1 day, no discount");
        Console.WriteLine(service.Rent(amina, CarRegistration, 1));
        PrintCounter("expected 3");

        Section("9. All rentals");
        PrintAllRentals(service);

        Section("10. Prediction experiment (new vs override)");
        RunHidingExperiment(car);
        PrintCounter("unchanged, still 3");
    }

    private static void PrintCounter(string note = "")
    {
        string suffix = note.Length == 0 ? "" : $"  ({note})";
        Console.WriteLine($"Counter: {RentalService.SuccessfulRentalCount}{suffix}");
    }

    private static void PrintAllRentals(RentalService service)
    {
        foreach (Rental rental in service.Rentals)
            Console.WriteLine(rental);

        decimal totalOfAll = service.Rentals.Sum(r => r.Total);
        Console.WriteLine($"Sum of original totals: {totalOfAll:N0} EGP  (expected 2,500)");
    }

    // Predictions, written before the first run (also in the README):
    //   1. car.GetCategoryLabel()             -> "Car"      hidden member, chosen by the compile-time type Car
    //   2. vehicle.GetCategoryLabel()         -> "Vehicle"  non-virtual, chosen by the compile-time type Vehicle
    //   3. ((Vehicle)car).GetCategoryLabel()  -> "Vehicle"  the cast changes the reference type, not the object
    //   4. vehicle.GetDescription()           -> "Car | CAR-01: Toyota City"  virtual + override: runtime type wins
    //   5. pricedItem.CalculateCost(3)        -> 1600       interface -> Vehicle wrapper -> Car.CalculateCostCore
    private static void RunHidingExperiment(Car car)
    {
        Vehicle vehicle = car;
        IPriceable pricedItem = car;

        Console.WriteLine(car.GetCategoryLabel());
        Console.WriteLine(vehicle.GetCategoryLabel());
        Console.WriteLine(((Vehicle)car).GetCategoryLabel());
        Console.WriteLine(vehicle.GetDescription());
        Console.WriteLine(pricedItem.CalculateCost(3));
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
    }

    // ============================================================ REJECTIONS

    // Runs an operation that must be rejected and reports whether the service state stayed identical.
    // The catch only observes the rejection; nothing is mutated afterwards.
    private static (bool WasRejected, Exception? Error, bool StateUnchanged) TryRejected(
        RentalService service, Action operation)
    {
        string stateBefore = DescribeState(service);
        try
        {
            operation();
            return (false, null, StateUnchanged: stateBefore == DescribeState(service));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return (true, ex, StateUnchanged: stateBefore == DescribeState(service));
        }
    }

    // A snapshot of everything a rejection must not touch: rentals, counter, availability.
    private static string DescribeState(RentalService service)
    {
        IEnumerable<string> availability = service.Vehicles.Select(v =>
            $"{v.Registration}={(service.IsAvailable(v) ? "available" : "rented")}");

        return $"rentals={service.Rentals.Count}, counter={RentalService.SuccessfulRentalCount}, " +
               $"availability=[{string.Join(", ", availability)}]";
    }

    private static void Reject(RentalService service, string label, Action operation)
    {
        var (wasRejected, error, stateUnchanged) = TryRejected(service, operation);

        if (!wasRejected)
        {
            Console.WriteLine($"[FAIL] {label}: expected a rejection but it was accepted.");
            _failureCount++;
            return;
        }

        Console.WriteLine($"[REJECTED] {label}");
        Console.WriteLine($"           {error!.GetType().Name}: {FirstLine(error.Message)}");
        Console.WriteLine($"           state {(stateUnchanged ? "unchanged" : "CHANGED!")} -> {DescribeState(service)}");
        if (!stateUnchanged) _failureCount++;
    }

    private static string FirstLine(string text) => text.Split('\n')[0].Trim();

    // ============================================================== CHECKS

    private static void RunChecks()
    {
        int counterAtStart = RentalService.SuccessfulRentalCount;
        var (service, amina, car, bike) = CreateScenario();

        CheckPricing(service, car, bike, counterAtStart);
        CheckConstructorValidation();
        CheckDescriptions(car, bike);
        CheckRentalLifecycle(service, amina, car, counterAtStart);
        CheckReadOnlyViews(service);
        CheckSharedCounter(amina);
        CheckHidingExperiment(car);

        Console.WriteLine();
        Console.WriteLine(_failureCount == 0 ? "ALL CHECKS PASSED" : $"{_failureCount} CHECK(S) FAILED");
    }

    private static void CheckPricing(RentalService service, Car car, Motorcycle bike, int counterAtStart)
    {
        Expect(service.Quote(car, 3) == 1600m, "Quote car 3d = 1600");
        Expect(service.Quote(bike, 2) == 400m, "Quote bike 2d = 400");
        Expect(RentalService.SuccessfulRentalCount == counterAtStart, "Quotes do not change the counter");

        ExpectThrows<ArgumentOutOfRangeException>("Car 0 days rejected", () => car.CalculateCost(0));
        ExpectThrows<ArgumentOutOfRangeException>("Car -1 days rejected", () => car.CalculateCost(-1));
        ExpectThrows<ArgumentOutOfRangeException>("Bike 0 days rejected", () => bike.CalculateCost(0));
        ExpectThrows<ArgumentOutOfRangeException>("Bike -1 days rejected", () => bike.CalculateCost(-1));
        ExpectThrows<ArgumentNullException>("Quote(null) rejected", () => service.Quote(null!, 1));
    }

    private static void CheckConstructorValidation()
    {
        ExpectThrows<ArgumentOutOfRangeException>("Daily rate 0 rejected", () => new Motorcycle("X", "M", 0m));
        ExpectThrows<ArgumentOutOfRangeException>("Negative daily rate rejected", () => new Motorcycle("X", "M", -1m));
        ExpectThrows<ArgumentOutOfRangeException>("Negative one-time fee rejected", () => new Car("X", "M", 100m, -1m));
        Expect(new Car("X", "M", 100m, 0m).CalculateCost(1) == 100m, "Zero one-time fee allowed");

        ExpectThrows<ArgumentException>("Customer null ID rejected", () => new Customer(null!, "A"));
        ExpectThrows<ArgumentException>("Customer empty ID rejected", () => new Customer("", "A"));
        ExpectThrows<ArgumentException>("Customer whitespace name rejected", () => new Customer("C", "   "));
    }

    private static void CheckDescriptions(Car car, Motorcycle bike)
    {
        Expect(car.GetDescription() == "Car | CAR-01: Toyota City", "Car description");
        Expect(bike.GetDescription() == "Motorcycle | BIKE-01: Honda Street", "Motorcycle description");
    }

    private static void CheckRentalLifecycle(RentalService service, Customer amina, Car car, int counterAtStart)
    {
        // Accepted rentals (the bike uses messy casing/spaces on purpose).
        Rental carRental = service.Rent(amina, CarRegistration, 3, 100m);
        Expect(carRental.Subtotal == 1600m && carRental.Discount == 100m && carRental.Total == 1500m,
            "Rent car: 1600 - 100 = 1500");

        Rental bikeRental = service.Rent(amina, "  bike-01 ", 2);
        Expect(bikeRental.Total == 400m && bikeRental.Discount == 0m, "Rent bike (3-arg, messy registration): total 400");
        Expect(RentalService.SuccessfulRentalCount == counterAtStart + 2, "Counter = start + 2");
        Expect(carRental.Id > 0 && bikeRental.Id == carRental.Id + 1, "Rental IDs are positive and sequential");

        // Rejections while the car is Active.
        ExpectRejectedWithoutChange(service, "Rent an Active car", () => service.Rent(amina, CarRegistration, 1));
        ExpectRejectedWithoutChange(service, "Rent unknown registration", () => service.Rent(amina, "ZZZ", 1));
        ExpectRejectedWithoutChange(service, "Rent null registration", () => service.Rent(amina, null!, 1));
        ExpectRejectedWithoutChange(service, "Rent null customer", () => service.Rent(null!, CarRegistration, 1));
        ExpectRejectedWithoutChange(service, "Add duplicate ' car-01 '", () => service.AddVehicle(new Car(" car-01 ", "Dup", 1m, 0m)));
        ExpectRejectedWithoutChange(service, "Add null vehicle", () => service.AddVehicle(null!));
        ExpectRejectedWithoutChange(service, "Return unknown ID 999", () => service.Return(999));
        ExpectRejectedWithoutChange(service, "Return ID 0", () => service.Return(0));
        ExpectRejectedWithoutChange(service, "Return ID -1", () => service.Return(-1));

        // Return.
        service.Return(carRental.Id);
        Expect(carRental.Status == RentalStatus.Returned, "First rental is Returned");
        Expect(service.IsAvailable(car), "Car is available after the return");
        Expect(carRental.Total == 1500m, "Returned rental keeps its original total");
        Expect(RentalService.SuccessfulRentalCount == counterAtStart + 2, "Return does not change the counter");
        ExpectRejectedWithoutChange(service, "Second return of the same rental", () => service.Return(carRental.Id));

        // Rejections while the car is available.
        ExpectRejectedWithoutChange(service, "Rent for 0 days", () => service.Rent(amina, CarRegistration, 0));
        ExpectRejectedWithoutChange(service, "Rent for -1 days", () => service.Rent(amina, CarRegistration, -1));
        ExpectRejectedWithoutChange(service, "Negative discount", () => service.Rent(amina, CarRegistration, 1, -1m));
        ExpectRejectedWithoutChange(service, "Discount greater than the quote", () => service.Rent(amina, CarRegistration, 1, 601m));
        Expect(service.IsAvailable(car), "Car is still available after the rejections");

        // Discount boundaries: 0 and exactly the subtotal are both accepted.
        Rental noDiscount = service.Rent(amina, CarRegistration, 1, 0m);
        Expect(noDiscount.Total == 600m, "Discount 0 accepted (total 600)");
        service.Return(noDiscount.Id);

        Rental fullDiscount = service.Rent(amina, CarRegistration, 1, 600m);
        Expect(fullDiscount.Total == 0m, "Discount equal to the subtotal accepted (total 0)");
        service.Return(fullDiscount.Id);
    }

    private static void CheckReadOnlyViews(RentalService service)
    {
        Expect(service.Rentals as List<Rental> is null, "Rentals view cannot be cast to List<Rental>");
        Expect(service.Vehicles as List<Vehicle> is null, "Vehicles view cannot be cast to List<Vehicle>");
        ExpectThrows<NotSupportedException>("Mutating the Rentals view throws",
            () => ((ICollection<Rental>)service.Rentals).Add(service.Rentals[0]));
    }

    private static void CheckSharedCounter(Customer amina)
    {
        int counterBefore = RentalService.SuccessfulRentalCount;

        var secondService = new RentalService();
        Expect(RentalService.SuccessfulRentalCount == counterBefore, "A second service does not reset the counter");

        secondService.AddVehicle(new Motorcycle("M-2", "Test", 10m));
        secondService.Rent(amina, "M-2", 1);
        Expect(RentalService.SuccessfulRentalCount == counterBefore + 1, "The counter is shared across services");
    }

    private static void CheckHidingExperiment(Car car)
    {
        Vehicle vehicle = car;

        Expect(car.GetCategoryLabel() == "Car", "Experiment 1: car.GetCategoryLabel() = Car");
        Expect(vehicle.GetCategoryLabel() == "Vehicle", "Experiment 2: vehicle.GetCategoryLabel() = Vehicle");
        Expect(((Vehicle)car).GetCategoryLabel() == "Vehicle", "Experiment 3: a cast gives Vehicle");
        Expect(vehicle.GetDescription() == "Car | CAR-01: Toyota City", "Experiment 4: description uses the runtime type");
        Expect(((IPriceable)car).CalculateCost(3) == 1600m, "Experiment 5: interface cost = 1600");
    }

    // ----------------------------------------------------------- assertions

    private static void Expect(bool condition, string label)
    {
        Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {label}");
        if (!condition) _failureCount++;
    }

    private static void ExpectThrows<TException>(string label, Action action) where TException : Exception
    {
        try
        {
            action();
            Expect(false, $"{label} (no exception thrown)");
        }
        catch (TException)
        {
            Expect(true, label);
        }
        catch (Exception ex)
        {
            Expect(false, $"{label} (wrong exception: {ex.GetType().Name})");
        }
    }

    private static void ExpectRejectedWithoutChange(RentalService service, string label, Action operation)
    {
        var (wasRejected, _, stateUnchanged) = TryRejected(service, operation);
        Expect(wasRejected && stateUnchanged, $"{label}: rejected with no state change");
    }

    // Deliberately invalid code, kept as comments (each one fails to compile):
    //   rental.Status = RentalStatus.Returned;     // CS0272: set accessor is inaccessible
    //   var v = new Vehicle("X", "Y", 1m);         // CS0144: cannot create an instance of an abstract type
    //   class Sedan : Car { }                      // CS0509: cannot derive from sealed type 'Car'
    //   var p = new Program();                     // CS0723: cannot declare a variable of static type
    //   RentalService.SuccessfulRentalCount = 0;   // CS0272: set accessor is inaccessible
    //   car.CalculateCostCore(3);                  // CS1061: inaccessible because of its protection level
    //   (List<Rental>)service.Rentals              // compiles, but throws InvalidCastException at runtime
}
