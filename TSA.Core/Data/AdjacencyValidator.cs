namespace TSA.Core.Data;

/// <summary>
/// Sanity checks for countries.json: every connection must exist and be two-way.
/// </summary>
public static class AdjacencyValidator
{
    public static List<string> Validate(IReadOnlyList<CountryDefinition> countries)
    {
        var errors = new List<string>();
        var lookup = countries.ToDictionary(x => x.Id);

        foreach (var country in countries)
        {
            foreach (var adjacent in country.Adjacent)
            {
                if (adjacent == country.Id)
                {
                    errors.Add($"{country.Id} references itself");
                    continue;
                }

                if (!lookup.TryGetValue(adjacent, out var other))
                {
                    errors.Add($"{country.Id} references missing country '{adjacent}'");
                    continue;
                }

                if (!other.Adjacent.Contains(country.Id))
                {
                    errors.Add($"{country.Id} -> {adjacent} is not bidirectional");
                }
            }
        }

        return errors;
    }
}
