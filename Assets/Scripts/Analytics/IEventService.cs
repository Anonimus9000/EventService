using System.Threading.Tasks;

namespace Analytics
{
    public interface IEventService
    {
        void Initialize();
        void Deinitialize();
    }
}