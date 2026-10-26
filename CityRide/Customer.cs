public sealed class Customer
{
    private readonly string _id;
    private readonly string _name;

    public string Id => _id;
    public string Name => _name;

    public Customer(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _id = id.Trim();
        _name = name.Trim();
    }
}
