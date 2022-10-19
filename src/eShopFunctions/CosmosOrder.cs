using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace eShopFunctions;
public class CosmosOrder
{
    public int Id { get; set; }
    public string BuyerId { get; set; }
    public Address ShipToAddress { get; set; }
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.Now;

    public IEnumerable<OrderItem> OrderItems = new List<OrderItem>();

    public decimal Total()
    {
        var total = 0m;
        foreach (var item in OrderItems)
        {
            total += item.UnitPrice * item.Units;
        }
        return total;
    }

}

public class CosmosOrderExt
{
    public string id { get; set; }

    public string Id { get; set; }
    public int OrderId { get; set; }
    public string BuyerId { get; set; }
    public Address ShipToAddress { get; set; }
    public DateTimeOffset OrderDate { get; set; }

    public IEnumerable<OrderItem> OrderItems;

    public CosmosOrderExt(CosmosOrder data)
    {
        id = Guid.NewGuid().ToString().ToUpper();
        Id = id;
        if (data != null)
        {
            OrderId = data.Id;
            BuyerId = data.BuyerId;
            ShipToAddress = data.ShipToAddress;
            OrderDate = data.OrderDate;
            OrderItems = data.OrderItems;
        }
    }

    public decimal Total()
    {
        var total = 0m;
        foreach (var item in OrderItems)
        {
            total += item.UnitPrice * item.Units;
        }
        return total;
    }

}

public class Address // ValueObject
{
    public string Street { get; private set; }

    public string City { get; private set; }

    public string State { get; private set; }

    public string Country { get; private set; }

    public string ZipCode { get; private set; }

    private Address() { }

    public Address(string street, string city, string state, string country, string zipcode)
    {
        Street = street;
        City = city;
        State = state;
        Country = country;
        ZipCode = zipcode;
    }
}

public class OrderItem 
{
    public int Id { get; set; }
    public CatalogItemOrdered ItemOrdered { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Units { get; private set; }

    private OrderItem()
    {
        // required by EF
    }

    public OrderItem(CatalogItemOrdered itemOrdered, decimal unitPrice, int units)
    {
        ItemOrdered = itemOrdered;
        UnitPrice = unitPrice;
        Units = units;
    }
}

public class CatalogItemOrdered // ValueObject
{
    public int CatalogItemId { get; private set; }
    public string ProductName { get; private set; }
    public string PictureUri { get; private set; }
}

