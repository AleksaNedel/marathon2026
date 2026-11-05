using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class WaiterPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator OrderLines => Page.Locator(".preorder-list app-dish-card.pre-order-dish");

    public static ILocator DishName(ILocator line) => line.Locator(".dish-name");
    public static ILocator Quantity(ILocator line) => line.Locator(".pre-order-quantity");
}
