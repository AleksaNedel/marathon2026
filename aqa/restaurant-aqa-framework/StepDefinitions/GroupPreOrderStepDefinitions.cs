using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("the owner has made a group reservation at the configured table")]
    public Task GivenOwnerMadeGroupReservation() =>
        Actor.AttemptsToAsync(new SetUpGroupPreOrder());

    [Given("the owner has made a group reservation for the guest-removal UI flow")]
    [Scope(Tag = "ui")]
    public Task GivenOwnerMadeGroupReservationForUiFlow() =>
        Actor.AttemptsToAsync(new SetUpGroupPreOrder(createSecondDish: true));

    [Given("two guests add dishes and submit their carts through the invitation")]
    [Scope(Tag = "ui")]
    public Task GivenTwoGuestsSubmitTheirCartsThroughTheInvitation() =>
        Actor.AttemptsToAsync(new GuestsSubmitPreOrdersThroughInvitation());

    [When("the owner reviews the guests, removes one, and confirms the pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenOwnerRemovesGuestAndConfirmsPreOrder() =>
        Actor.AttemptsToAsync(new OwnerRemovesGuestAndSubmitsGroupPreOrder());

    [Then("the waiter sees only the remaining guest's dish in the order")]
    [Scope(Tag = "ui")]
    public async Task ThenWaiterSeesOnlyRemainingGuestDish()
    {
        await Actor.AttemptsToAsync(new WaiterOpensSubmittedGroupPreOrder());
        var guests = Recall<GroupPreOrderUiGuests>();
        var lines = await Actor.AsksForAsync(new TheWaiterPreOrder());

        await Assert.That(lines).Count().IsEqualTo(1);
        await Assert.That(lines.Select(line => line.DishName))
            .Contains(guests.RemainingDishName);
        await Assert.That(lines.Select(line => line.DishName))
            .DoesNotContain(guests.RemovedDishName);
        await Assert.That(lines[0].Quantity).IsEqualTo("Quantity: 1");
    }

    [Given("one guest has joined by invitation")]
    public Task GivenOneGuestJoined() =>
        Actor.AttemptsToAsync(new JoinGroupPreOrder());

    [Given("two guests have joined by invitation")]
    public Task GivenTwoGuestsJoined() =>
        Actor.AttemptsToAsync(new JoinGroupPreOrder(count: 2));

    [Given("the guests submitted their carts")]
    public Task GivenGuestsSubmittedCarts()
    {
        var guests = Recall<IReadOnlyList<GroupPreOrderGuest>>();
        return Actor.AttemptsToAsync(new SubmitGroupGuestCarts(guests.Count));
    }

    [When("the owner reviews the participants")]
    public Task WhenOwnerReviewsParticipants() =>
        Actor.AttemptsToAsync(new ListGroupParticipants());

    [Then("both guests are listed with submitted carts")]
    public async Task ThenBothGuestsAreListedWithSubmittedCarts()
    {
        var response = await Actor.AsksForAsync(new TheGroupParticipants());
        var guests = Recall<IReadOnlyList<GroupPreOrderGuest>>();

        await Assert.That(response.Participants.Count).IsEqualTo(2).Because($"response body was: {Actor.TheLastResponse().Body}");
        await Assert.That(response.Participants.All(participant => participant.IsSubmitted))
            .IsTrue()
            .Because($"response body was: {Actor.TheLastResponse().Body}");
        await Assert.That(response.Participants.Select(participant => participant.Id))
            .Contains(guests[0].SessionId)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
        await Assert.That(response.Participants.Select(participant => participant.Id))
            .Contains(guests[1].SessionId)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
    }

    [Given("the guest submitted a cart")]
    public Task GivenGuestSubmittedCart() =>
        Actor.AttemptsToAsync(new SubmitGroupGuestCarts(1));

    [Given("the owner has made another group reservation at the configured table")]
    public Task GivenOwnerMadeAnotherGroupReservation() =>
        Actor.AttemptsToAsync(new SetUpGroupPreOrder("other-group"));

    [Given("a guest has joined the other group by invitation")]
    public Task GivenGuestJoinedOtherGroup() =>
        Actor.AttemptsToAsync(new JoinGroupPreOrder(
            memoryName: "other-group",
            guestMemoryName: "other-group-guest"));

    [When("the owner removes the first guest")]
    public Task WhenOwnerRemovesFirstGuest() =>
        Actor.AttemptsToAsync(new DeleteGroupParticipant("first"));

    [When("the owner removes the only guest")]
    public Task WhenOwnerRemovesOnlyGuest() =>
        Actor.AttemptsToAsync(new DeleteGroupParticipant("only"));

    [When("the owner tries to remove a guest from another group")]
    public Task WhenOwnerRemovesGuestFromAnotherGroup() =>
        Actor.AttemptsToAsync(new DeleteGroupParticipant("different group"));

    [When("the owner tries to remove a participant who does not exist")]
    public Task WhenOwnerRemovesMissingParticipant() =>
        Actor.AttemptsToAsync(new DeleteGroupParticipant("missing"));

    [When("{string} tries to remove the guest")]
    public Task WhenPersonaTriesToRemoveGuest(string persona) =>
        Actor.AttemptsToAsync(new TryToRemoveGroupParticipant(persona));

    [Then("the other guest remains and the total reflects their cart")]
    public async Task ThenOtherGuestRemainsAndTotalIsRecalculated()
    {
        var response = await Actor.AsksForAsync(new TheGroupParticipants());
        var guests = Recall<IReadOnlyList<GroupPreOrderGuest>>();
        var reservation = Recall<GroupPreOrderReservation>();

        await Assert.That(response.Participants.Select(participant => participant.Id))
            .Contains(guests[1].SessionId)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
        await Assert.That(response.Total)
            .IsEqualTo(reservation.Dish.Price * 2)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
    }

    [Then("the removed guest is absent and the total is zero")]
    public async Task ThenRemovedGuestIsAbsentAndTotalIsZero()
    {
        var response = await Actor.AsksForAsync(new TheGroupParticipants());
        var removedGuest = Recall<IReadOnlyList<GroupPreOrderGuest>>()[0];

        await Assert.That(response.Participants.Select(participant => participant.Id))
            .DoesNotContain(removedGuest.SessionId)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
        await Assert.That(response.Total)
            .IsEqualTo(0m)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
    }

    [Then("the group has no participants and the total is zero")]
    public async Task ThenGroupHasNoParticipantsAndTotalIsZero()
    {
        var response = await Actor.AsksForAsync(new TheGroupParticipants());
        await Assert.That(response.Participants)
            .IsEmpty()
            .Because($"response body was: {Actor.TheLastResponse().Body}");
        await Assert.That(response.Total)
            .IsEqualTo(0m)
            .Because($"response body was: {Actor.TheLastResponse().Body}");
    }
}
