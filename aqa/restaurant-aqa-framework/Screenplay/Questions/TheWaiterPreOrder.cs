using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record WaiterPreOrderLine(string DishName, string Quantity);

public sealed class TheWaiterPreOrder : IQuestion<IReadOnlyList<WaiterPreOrderLine>>
{
    public async Task<IReadOnlyList<WaiterPreOrderLine>> AnsweredByAsync(Actor actor)
    {
        var page = new WaiterPreOrderPage(actor.LookingAtThePage());
        var orderLines = page.OrderLines;
        await orderLines.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var lines = new List<WaiterPreOrderLine>();
        for (var index = 0; index < await orderLines.CountAsync(); index++)
        {
            var line = orderLines.Nth(index);
            lines.Add(new WaiterPreOrderLine(
                (await WaiterPreOrderPage.DishName(line).InnerTextAsync()).Trim(),
                (await WaiterPreOrderPage.Quantity(line).InnerTextAsync()).Trim()));
        }

        return lines;
    }
}
