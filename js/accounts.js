$(":checkbox").on('click', function(event) {
    let roleName = $(event.target).attr("data-role-id");
    let status = $(event.target).is(":checked");
    let userId = $(event.target).closest("tr").attr('data-user-id');

    $.ajax({
        url: "administration/accounts/" + userId + "/update-roles/" + ((status) ? "grant/" : "revoke/") + roleName,
        type: 'POST',
        error: function(result, message, error) {
            alert("An error occured: " + error);
        }
    });
});
