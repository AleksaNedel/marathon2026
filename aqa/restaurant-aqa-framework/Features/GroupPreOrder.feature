Feature: Group pre-orders

  A reservation owner can review guests' submitted carts and remove a participant
  before sending the group pre-order to the restaurant.

  Rule: The owner can remove a guest and the group total is recalculated

    @ui @group-preorder-ui @known-defect
    Scenario: The waiter sees the group order without the removed guest
      Given the owner has made a group reservation for the guest-removal UI flow
      And two guests add dishes and submit their carts through the invitation
      When the owner reviews the guests, removes one, and confirms the pre-order
      Then the waiter sees only the remaining guest's dish in the order

    @api @group-preorder
    Scenario: Removing one guest leaves the other guest's submitted cart
      Given the owner has made a group reservation at the configured table
      And two guests have joined by invitation
      And the guests submitted their carts
      When the owner reviews the participants
      Then both guests are listed with submitted carts
      When the owner removes the first guest
      Then the other guest remains and the total reflects their cart

    @api @group-preorder
    Scenario: The owner can remove a guest who submitted a cart
      Given the owner has made a group reservation at the configured table
      And one guest has joined by invitation
      And the guest submitted a cart
      When the owner removes the only guest
      Then the removed guest is absent and the total is zero

    @api @group-preorder
    Scenario: Removing the last guest leaves no participants and no total
      Given the owner has made a group reservation at the configured table
      And one guest has joined by invitation
      When the owner removes the only guest
      Then the group has no participants and the total is zero

  Rule: A participant can only be removed from their own group

    @api @group-preorder @negative
    Scenario: A guest from another group cannot be removed
      Given the owner has made a group reservation at the configured table
      And the owner has made another group reservation at the configured table
      And a guest has joined the other group by invitation
      When the owner tries to remove a guest from another group
      Then the participant removal is refused

    @api @group-preorder @negative
    Scenario: The owner cannot remove a participant who does not exist
      Given the owner has made a group reservation at the configured table
      When the owner tries to remove a participant who does not exist
      Then the participant cannot be found

  Rule: Only the reservation owner can remove a participant

    @api @group-preorder @negative
    Scenario Outline: Someone other than the owner cannot remove a guest
      Given the owner has made a group reservation at the configured table
      And one guest has joined by invitation
      When "<persona>" tries to remove the guest
      Then <outcome>

      Examples: Authenticated and unauthenticated visitors
        | persona                          | outcome                     |
        | the guest of the group           | the deletion is forbidden   |
        | another customer                 | the deletion is forbidden   |
        | the waiter assigned to the table | the deletion is forbidden   |
        | an unauthenticated visitor       | the visitor is unauthorized |
