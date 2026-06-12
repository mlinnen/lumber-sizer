using System.Threading.Tasks;
using WWA.Core.Models;

namespace WWA.Core.Interfaces
{
    /// <summary>
    /// Contract for a boarding packer. Implementations pack a CutList into available inventory according to Constraints.
    /// </summary>
    public interface IPacker
    {
        /// <summary>
        /// Pack the provided request asynchronously and return a PackingResult describing allocations and remnants.
        /// </summary>
        /// <param name="request">Packing request containing cutlist, inventory snapshot, constraints and options.</param>
        /// <returns>PackingResult with allocations, remnants and metrics.</returns>
        Task<PackingResult> PackAsync(PackingRequest request);
    }
}