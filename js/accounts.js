var $deletingAccount;

$(".delete-account-link").on('click', function(event) {
    if (!confirm("Do you really want to delete this user's account? No recordings or NPC settings will be deleted, but the voice actor of those NPCs will be set to NULL.")) {
        return;
    }

    let userId = $(event.target).attr('data-user-id');
    $deletingAccount = $(event.target).closest('tr');

    $.ajax({
        url: "administration/accounts/" + userId + "/delete",
        type: 'DELETE',
        success: function(result, message, error) {
            $deletingAccount.remove();
            $deletingAccount = undefined;
        },
        error: function(result, message, error) {
            $deletingAccount = undefined;
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
