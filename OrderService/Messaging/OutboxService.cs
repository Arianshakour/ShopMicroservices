using OrderService.Data;
using OrderService.Entities;
using System.Text.Json;

namespace OrderService.Messaging
{
    public class OutboxService
    {
        //In class faqat ye vazife dare
        //vaqti behesh yek Event bedim ono tabdil kone be json dar jadval OutboxMessages ezafe kone
        private readonly OrderDbContext _db;

        public OutboxService(OrderDbContext db)
        {
            _db = db;
        }

        public void Add<T>(T message)
        {
            var outboxMessage = new OutboxMessage
            {
                Type = typeof(T).Name,
                Payload = JsonSerializer.Serialize(message),
                CreatedAt = DateTime.UtcNow
            };

            _db.OutboxMessages.Add(outboxMessage);
        }
    }
}
