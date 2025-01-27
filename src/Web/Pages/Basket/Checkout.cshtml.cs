﻿using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;


namespace Microsoft.eShopWeb.Web.Pages.Basket;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IBasketService _basketService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOrderService _orderService;
    private string? _username = null;
    private readonly IBasketViewModelService _basketViewModelService;
    private readonly IAppLogger<CheckoutModel> _logger;
    static readonly HttpClient client = new HttpClient();
    private readonly IConfiguration _config;

    public CheckoutModel(IBasketService basketService,
        IBasketViewModelService basketViewModelService,
        SignInManager<ApplicationUser> signInManager,
        IOrderService orderService,
        IAppLogger<CheckoutModel> logger, IConfiguration config)
    {
        _basketService = basketService;
        _signInManager = signInManager;
        _orderService = orderService;
        _basketViewModelService = basketViewModelService;
        _logger = logger;
        _config = config;
    }

    public BasketViewModel BasketModel { get; set; } = new BasketViewModel();

    public async Task OnGet()
    {
        await SetBasketModelAsync();
    }

    public async Task<IActionResult> OnPost(IEnumerable<BasketItemViewModel> items)
    {
        try
        {
            await SetBasketModelAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var updateModel = items.ToDictionary(b => b.Id.ToString(), b => b.Quantity);
            await _basketService.SetQuantities(BasketModel.Id, updateModel);
            ApplicationCore.Entities.OrderAggregate.Order order = await _orderService.CreateOrderAsync(BasketModel.Id, new Address("123 Main St.", "Kent", "OH", "United States", "44240"));
           
            var sbSendResult = await SendOrderToFunctionServiceBus(order);
            var weburlSendResult = await SendOrderToFunctionUrl(order);

            if (!sbSendResult && !weburlSendResult)
            {
                RedirectToPage("/Basket/Index");
            }

            await _basketService.DeleteBasketAsync(BasketModel.Id);
        }
        catch (EmptyBasketOnCheckoutException emptyBasketOnCheckoutException)
        {
            //Redirect to Empty Basket page
            _logger.LogWarning(emptyBasketOnCheckoutException.Message);
            return RedirectToPage("/Basket/Index");
        }

        return RedirectToPage("Success");
    }

    private async Task<bool> SendOrderToFunctionUrl(ApplicationCore.Entities.OrderAggregate.Order order)
    {
        string azureFunctionUrl = _config.GetValue(typeof(string), "OrderAzureFunctionUrl") as string;
        string functionKeys = _config.GetValue(typeof(string), "OrderAzureFunctionKeys") as string;
        using (var content = new StringContent(JsonConvert.SerializeObject(order), System.Text.Encoding.UTF8, "application/json"))
        {
            try
            {
                //HttpResponseMessage response = await client.PostAsync($"{azureFunctionUrl}?orderId={order.Id}", content);
                var HttpRequestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{azureFunctionUrl}?orderId={order.Id}"),
                    Headers =
                        {
                            { "x-functions-key", functionKeys }
                        },
                    Content = content
                };

                var response = await client.SendAsync(HttpRequestMessage);

                string result = response.Content.ReadAsStringAsync().Result;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                return false;
            }
        }
    }

    private async Task<bool> SendOrderToFunctionServiceBus(ApplicationCore.Entities.OrderAggregate.Order order)
    {
        string serviceBusConnectionString = _config.GetValue(typeof(string), "servicebusconnectionstr") as string;
        string queueName = "ordermessages1";
        
        await using var client = new ServiceBusClient(serviceBusConnectionString);

        await using ServiceBusSender sender = client.CreateSender(queueName);
        try
        {
            string messageBody = JsonConvert.SerializeObject(order);
            var message = new ServiceBusMessage(messageBody);
            Console.WriteLine($"Sending message: {messageBody}");
            await sender.SendMessageAsync(message);
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
            return false;
        }
        finally
        {
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    private async Task SetBasketModelAsync()
    {
        if (_signInManager.IsSignedIn(HttpContext.User))
        {
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(User.Identity.Name);
        }
        else
        {
            GetOrSetBasketCookieAndUserName();
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(_username);
        }
    }

    private void GetOrSetBasketCookieAndUserName()
    {
        if (Request.Cookies.ContainsKey(Constants.BASKET_COOKIENAME))
        {
            _username = Request.Cookies[Constants.BASKET_COOKIENAME];
        }
        if (_username != null) return;

        _username = Guid.NewGuid().ToString();
        var cookieOptions = new CookieOptions();
        cookieOptions.Expires = DateTime.Today.AddYears(10);
        Response.Cookies.Append(Constants.BASKET_COOKIENAME, _username, cookieOptions);
    }
}
