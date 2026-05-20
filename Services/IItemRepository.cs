using Pr3.ConfigAndSecurity.Domain;

namespace Pr3.ConfigAndSecurity.Services;

public interface IItemRepository
{
    IReadOnlyCollection<Item> GetAll();

    Item? GetById(Guid id);

    Item Create(string name, decimal price);
}
