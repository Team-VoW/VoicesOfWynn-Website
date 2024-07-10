var $affectedAccount;

$(".reset-password-link").on('click', function(event) {
    let userId = $(event.target).closest("tr").attr("data-user-id");
    $.ajax({
        url: "administration/accounts/" + userId + "/reset-password",
        type: 'PUT',
        success: function(result, message, error) {
            alert(result);
        },
        error: function(result, message, error) {
            alert("An error ocurred: " + error);
        }
    })
})

$(".clear-bio-link").on('click', function(event) {
    let userId = $(event.target).closest("tr").attr("data-user-id");
    $.ajax({
        url: "administration/accounts/" + userId + "/clear-bio",
        type: 'PUT',
        success: function(result, message, error) {
            alert("Bio of the user cleared.");
        },
        error: function(result, message, error) {
            alert("An error ocurred: " + error);
        }
    })
});

$(".clear-avatar-link").on('click', function(event) {
    let userId = $(event.target).closest("tr").attr("data-user-id");
    $affectedAccount = $(event.target).closest("tr");
    $.ajax({
        url: "administration/accounts/" + userId + "/clear-avatar",
        type: 'PUT',
        success: function(result, message, error) {
            $affectedAccount.find(".avatar").attr("src", "dynamic/avatars/default.png");
            alert("Avatar of the user cleared.");
        },
        error: function(result, message, error) {
            alert("An error ocurred: " + error);
        }
    })
});

$(".delete-account-link").on('click', function(event) {
    if (!confirm("Do you really want to delete this user's account? No recordings or NPC settings will be deleted, but the voice actor of those NPCs will be set to NULL.")) {
        return;
    }

    let userId = $(event.target).closest("tr").attr("data-user-id");
    $affectedAccount = $(event.target).closest('tr');

    $.ajax({
        url: "administration/accounts/" + userId + "/delete",
        type: 'DELETE',
        success: function(result, message, error) {
            $affectedAccount.remove();
            $affectedAccount = undefined;
        },
        error: function(result, message, error) {
            $affectedAccount = undefined;
            alert("An error occurred: " + error);
        }
    });
});

$(":checkbox").on('click', function(event) {
    let roleId = $(event.target).attr("data-role-id");
    let status = $(event.target).is(":checked");
    let userId = $(event.target).closest("tr").attr('data-user-id');

    $.ajax({
        url: "administration/accounts/" + userId + "/update-roles/" + ((status) ? "grant/" : "revoke/") + roleId,
        type: 'POST',
        error: function(result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});
