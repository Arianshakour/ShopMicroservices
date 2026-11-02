using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Entities;

namespace ProductService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ProductDbContext _db;

        public ProductsController(ProductDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var products = _db.Products.ToList();

            return Ok(products);
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var product = _db.Products.FirstOrDefault(x => x.Id == id);
            if (product == null)
                return NotFound();

            return Ok(product);
        }
        [HttpPost]
        public IActionResult Create(Product product)
        {
            _db.Products.Add(product);

            _db.SaveChanges();

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                product);
        }
    }
}
