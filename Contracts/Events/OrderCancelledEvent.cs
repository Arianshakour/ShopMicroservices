using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contracts.Events
{
    //Kolan in class va baqie class haei ke dar in project Contracts misazim baraye ertebat 2 service ba ham hast
    //Ke 2 service be ham vabaste nabashan
    //event ha payam ha va dto haye moshtarak mizarim to in project

    //In Event miad dar jadval OrderInventoryState sefaresh haye ke cancelling hastano mikone cancelled
    public class OrderCancelledEvent
    {
        public int OrderId { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
