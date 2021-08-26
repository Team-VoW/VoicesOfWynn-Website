//var npcId; - filled by PHP in the view
var commentItemHtml = '<div class="card" style="display:none;"><span><strong>{name}</strong> < {email} ></span><hr><p>{comment}</p></div>';

$("#new-comment-button").on('click', function () {
    $("form").slideDown(1500);
    $("#new-comment-button").hide();
    $("#hide-form-button").show();
})
$("#hide-form-button").on('click', function () {
    $("form").slideUp(1500);
    $("#hide-form-button").hide();
    $("#new-comment-button").show();
})

$("form").on('submit', function (event) {
    event.preventDefault();
    let recordingId = $(event.target).attr('data-recording-id');
    let name = $("#name").val();
    let email = $("#email").val();
    let content = $("#content").val();
    let antispam = $("#antispam").val();

    $.ajax({
        url: "contents/npc/" + npcId + "/comments/" + recordingId + "/new",
        type: 'POST',
        data: {
            'name': name,
            'email': email,
            'content': content,
            'antispam': antispam
        },
        success: function (result, message) {
            let name = $("#name").val();
            if (name === '') {
                name = 'Anonymous';
            }
            let email = $("#email").val();
            if (email === '') {
                email = 'nobody@nowhere.net';
            }
            email = email.replace('@', ' at ').replace('.', ' dot ');

            let comment = commentItemHtml.replace('{name}', name);
            comment = comment.replace('{email}', email);
            comment = comment.replace('{comment}', $("#content").val());
            $("#comments").prepend($(comment));
            $("#hide-form-button").click();
            $("#comments :first-child").fadeIn(3500);
            $("#content").val("");

        },
        error: function (result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});

var $deletingComment;
$(".delete-comment-button").on('click', function (event) {
    if (!confirm('Do you really want to delete this comment?')) {
        return;
    }

    $deletingComment = $(event.target).closest('.card');

    $.ajax({
        url: "contents/npc/" + npcId + "/comments/" + $("form").attr('data-recording-id') + "/delete/" + $(event.target).attr('data-comment-id'),
        type: 'DELETE',
        success: function (result, message) {
            $deletingComment.slideUp(500);
            $deletingComment = undefined;
        },
        error: function (result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});