# CityRide — C# Console OOP Challenge

## Build / Run

```bash
dotnet build
dotnet run              # scripted demo (fresh process)
dotnet run -- --check   # repeatable checks (exit code 0 = all passed)
```

Requires .NET 8 SDK. No external packages. (In my sandbox, NuGet was network-blocked, so I restored offline with `dotnet build -p:RestoreSources=<empty-folder>`. On a normal machine plain `dotnet build` is enough.)

## Project Structure

| File | Type |
|---|---|
| `IPriceable.cs` | interface (not counted) |
| `RentalStatus.cs` | enum (not counted) |
| `Vehicle.cs` | abstract class |
| `Car.cs` | sealed class |
| `Motorcycle.cs` | sealed class |
| `Customer.cs` | sealed class |
| `Rental.cs` | sealed class |
| `RentalService.cs` | sealed class |
| `Program.cs` | static class (Main, demo, `--check` helpers) |

7 classes total. No records, no nested types, no custom exceptions, no test classes.

## Clean-code notes

- Constructor validation uses the built-in guards (`ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`, `ArgumentNullException.ThrowIfNull`). Explicit `throw` is kept only where a domain-specific message matters (days, discount).
- `RentalService.Rent` reads as validate → build → commit: `GetAvailableVehicle`, `EnsureValidDiscount`, `NextRentalId` are small private helpers.
- One place decides registration matching (`FindVehicle` + `IsSameRegistration`), and one place decides availability (`IsAvailable`).
- `Program` is split into small named methods (`CreateScenario`, `RunDemo`, `RunChecks` → `CheckPricing`, `CheckRentalLifecycle`, ...). `TryRejected` is the single helper behind both the demo's `Reject` and the checks' `ExpectRejectedWithoutChange`.
- `TreatWarningsAsErrors` is on: the build has 0 warnings.

## Decisions (points the task leaves open)

- **Customer validation:** ID and name must be non-null and not empty/whitespace → `ArgumentException`. Values are stored trimmed.
- **Vehicle validation:** registration/model non-blank (`ArgumentException`); daily rate > 0 and Car one-time fee >= 0 (`ArgumentOutOfRangeException`).
- **Exceptions:** `ArgumentNullException` (null args), `ArgumentOutOfRangeException` (days, discount, rate, fee), `ArgumentException` (blank strings), `InvalidOperationException` (unknown registration/ID, not available, duplicate, double return).
- **Registration:** stored trimmed; every lookup goes through one private `FindVehicle` (trim + `OrdinalIgnoreCase`), used by `AddVehicle`, `Rent`.
- **Rental ID:** `SuccessfulRentalCount + 1`, assigned only after validation passes, so a rejected request never consumes an ID. Because the counter is static, a second service continues the sequence (IDs stay unique per process).
- **Rental stores strings** (customer id/name, registration), not objects: the contract is a frozen record of what happened; holding a `Vehicle` object would couple it to something that could change.
- **Discount boundaries:** `0 <= discount <= subtotal`; both 0 and exactly-subtotal are accepted.

## Output predictions (written before running the experiment)

| # | Expression | Prediction | Why |
|---|---|---|---|
| 1 | `car.GetCategoryLabel()` | `Car` | variable type is `Car` → the hiding `new` method is chosen at compile time |
| 2 | `vehicle.GetCategoryLabel()` | `Vehicle` | variable type is `Vehicle`; the method is non-virtual, so no runtime lookup |
| 3 | `((Vehicle)car).GetCategoryLabel()` | `Vehicle` | the cast changes the compile-time type, not the object |
| 4 | `vehicle.GetDescription()` | `Car \| CAR-01: Toyota City` | `virtual` + `override` → runtime type decides |
| 5 | `pricedItem.CalculateCost(3)` | `1600` | interface → `Vehicle.CalculateCost` (validates) → `Car.CalculateCostCore` = 3×500+100 |

**Actual output:** `Car`, `Vehicle`, `Vehicle`, `Car | CAR-01: Toyota City`, `1600` — all five matched.

## Demo transcript

Real output of `dotnet run` from a fresh process: see `demo_transcript.txt`. Key numbers: quotes 1,600 / 400 (counter 0); rentals 1,500 and 400 (counter 2); after return + new rental 600 (counter 3); sum of original totals 2,500; every rejection printed `state unchanged`.

## Requirement → code

| Requirement | Where |
|---|---|
| `IPriceable`, `RentalStatus` as given | `IPriceable.cs`, `RentalStatus.cs` |
| Vehicle: private readonly fields, public getters, `base(...)` | `Vehicle.cs`, `Car.cs`, `Motorcycle.cs` |
| Non-virtual `CalculateCost` → `protected abstract CalculateCostCore` | `Vehicle.CalculateCost` / `CalculateCostCore` |
| Car = days × rate + fee; Motorcycle = days × rate | `Car.CalculateCostCore`, `Motorcycle.CalculateCostCore` |
| Descriptions with prefix, reusing base | `GetDescription` overrides |
| `GetCategoryLabel` hidden with `new` | `Vehicle.GetCategoryLabel`, `Car.GetCategoryLabel` |
| Customer validation, readonly | `Customer.cs` |
| Rental fixed values, `Status` private setter, `Return()` once | `Rental.cs` |
| Private readonly lists + `AsReadOnly()` views | `RentalService.Vehicles/Rentals` |
| `Quote(IPriceable, int)` uses the interface | `RentalService.Quote` (also used by `Rent`) |
| 3-arg `Rent` delegates to 4-arg with `0m` | `RentalService.Rent` |
| Availability from Active rentals only | `RentalService.IsAvailable` |
| Static counter, `private set`, increments after acceptance | `RentalService.SuccessfulRentalCount` |
| Validate before mutating | `Rent`: validation block → build → commit (store, then counter) |
| Demo with real operations, no hard-coded report | `Program.RunDemo` |
| `--check` | `Program.RunChecks` |

## Compile-time errors (kept as comments, never compiled)

```csharp
rental.Status = RentalStatus.Returned;   // CS0272: set accessor inaccessible
var v = new Vehicle("X", "Y", 1m);       // CS0144: cannot create instance of abstract type
class Sedan : Car { }                    // CS0509: cannot derive from sealed type
var p = new Program();                   // CS0723: static type cannot be a variable type
RentalService.SuccessfulRentalCount = 0; // CS0272: set accessor inaccessible
car.CalculateCostCore(3);                // CS0122: protected, inaccessible
// (List<Rental>)service.Rentals         // compiles, throws InvalidCastException at runtime
```

## Concepts (شرح مختصر)

- **Abstraction (`Vehicle` vs `IPriceable`):** `IPriceable` = عقد التسعير بس، أي حاجة ليها سعر تنفع تتسعّر. `Vehicle` = أساس مشترك فيه state (registration/model/rate) وvalidation وجزء ناقص (`CalculateCostCore`). `Quote` و`Rent` بياخدوا/بيستخدموا `IPriceable` فمش محتاجين يعرفوا النوع.
- **Polymorphism + Template Method:** `CalculateCost` non-virtual: بتعمل validation للـ days في مكان واحد وبعدين تنادي الـ hook. الـ hook `protected` عشان محدش من برّا يتخطى الـ validation. إضافة `Truck` = class جديد بس، من غير تعديل في أي مكان تاني.
- **Static:** العداد مشترك للـ type كله، فلو عملت `RentalService` تاني ميترصّفرش (اتأكدت منه في `--check`). `static Program` مينفعش تعمل منه instance لأنه مجرد container للـ entry point.
- **Readonly:** `readonly` بيثبّت الـ reference مش المحتوى. الـ service لسه بتعمل `Add` على الـ list من جوه. `AsReadOnly()` بيدي wrapper مينفعش تـ cast منه لـ `List<T>`، لكنه مش deeply immutable: الـ objects جواه نفسها لسه objects عادية (محميّة هنا لأن `Rental` و`Vehicle` fields بتاعتهم readonly).
- **Access modifiers:** `Status` بـ `public get; private set` يعني أي حد يقرأ، بس `Rental` نفسها هي اللي تغيّره (عن طريق `Return()`). الـ property الـ public مش لازم تكون writable.
- **new / override / overload / virtual:** `override` = runtime، لأن الـ method `virtual` والـ object الحقيقي هو اللي يحدد. `new` في الـ hiding = compile-time، حسب نوع المتغيّر. `new Car(...)` في الإنشاء حاجة تانية خالص: بتنشئ object. الـ overload = نفس الاسم بـ parameters مختلفة؛ الـ 3-args بتنادي الـ 4-args بـ `0m` عشان مسار validation يبقى واحد.
- **Sealed:** `sealed class Car` = محدش يرث منها. `sealed override` = تقفل override بعينه جوه class ممكن يتورّث.
- **decimal:** للفلوس عشان الدقة العشرية.

## Verification done

- `dotnet build`: 0 warnings, 0 errors.
- `dotnet run`: demo output matches all expected numbers.
- `dotnet run -- --check`: all checks passed (exit code 0).
- Code search: no `is Car` / `is Motorcycle` / `switch` / `IsRented` in pricing or availability. `GetType()` appears only in `Program` to print exception names.
# CityRide
