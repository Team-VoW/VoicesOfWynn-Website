//var npcId; - filled by PHP in the view
//var voiceActorId - filled by PHP in the view
//var userId; - filled by PHP in the view
//var userName; - filled by PHP in the view
//var userAvatar; - filled by PHP in the view

var commentItemHTML = `
<div class="comment-card">
    <div class="comment-header">
        <img src="{gravatar}" alt="Avatar" class="comment-avatar"/>
        <div class="comment-author-info">
            <div class="comment-author-name">
                {name}
            </div>
            <div class="comment-badges">
                {badges}
            </div>
        </div>
        <button data-comment-id="{id}" class="delete-comment-button" title="Delete comment">×</button>
    </div>
    <hr class="comment-separator">
    <div class="comment-content">
        <p>{comment}</p>
    </div>
</div>
`;

$("#new-comment-button").on('click', function () {
    $(".comments-form-card").slideDown(1500);
    $("#new-comment-button").hide();
    $("#hide-form-button").show();
})
$("#hide-form-button").on('click', function () {
    $(".comments-form-card").slideUp(1500);
    $("#hide-form-button").hide();
    $("#new-comment-button").show();
})

$("#contributor-option").on('click', function () {
    $("#contributor-option").addClass('selected');
    $("#guest-option").removeClass('selected');
    $("#guest-form").hide();
    $("#contributor-form").show();
})
$("#guest-option").on('click', function () {
    $("#guest-option").addClass('selected');
    $("#contributor-option").removeClass('selected');
    $("#contributor-form").hide();
    $("#guest-form").show();
})

$("form").on('submit', function (event) {
    event.preventDefault();
    let recordingId = $(event.target).attr('data-recording-id');
    let name, email, content, antispam, verified;
    if ($("#contributor-option").length === 1 && $("#contributor-option").hasClass('selected')) {
        //Posting as a contributor
        verified = true;
        content = $("#content-contributor").val();
    }
    else {
        verified = false;
        name = $("#name").val();
        email = $("#email").val();
        content = $("#content-guest").val();
        antispam = $("#antispam").val();
    }

    $.ajax({
        url: "contents/npc/" + npcId + "/comments/" + recordingId + "/new",
        type: 'POST',
        data: {
            'verified': verified,
            'name': name,
            'email': email,
            'content': content,
            'antispam': antispam
        },
        success: function (result, message) {
            let name, badges = "", gravatar, content;
            if ($("#contributor-option").length === 1 && $("#contributor-option").hasClass('selected')) {
                name = "<a href='cast/" + userId + "'>" + userName + "</a>";
                gravatar = userAvatar;
                if (userId == voiceActorId) {
                    badges = "<span class=\"author-badge\" title=\"This user is the author of this recording.\">Author</span>";
                }
                badges += "\n<span class=\"contributor-badge\" title=\"This user contributed to this project.\">Contributor</span>";
                content = $("#content-contributor").val().replace(/\n/g, '<br>');
            }
            else {
                name = $("#name").val();
                if (name === '') {
                    name = 'Anonymous';
                }
                gravatar = "https://www.gravatar.com/avatar/" + md5(email) + "?d=identicon";
                badges = "";
                content = $("#content-guest").val().replace(/\n/g, '<br>');
            }

            let comment;
            comment = commentItemHTML.replace('{name}', name);
            comment = comment.replace('{gravatar}', gravatar);
            comment = comment.replace('{badges}', badges);
            comment = comment.replace('{id}', result); //Response from the server is just the number representing the ID of the new comment
            comment = comment.replace('{comment}', content);
            $comment = $(comment);
            $comment.find('.delete-comment-button').on('click', deleteComment);
            $comment.hide();
            $("#comments").prepend($comment);
            $comment.fadeIn(800);
            $("#hide-form-button").click();
            $("#content-contributor").val("");
            $("#content-guest").val("");

        },
        error: function (result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});

var $deletingComment;
$(".delete-comment-button").on('click', deleteComment);

function deleteComment(event) {
    if (!confirm('Do you really want to delete this comment?')) {
        return;
    }

    $deletingComment = $(event.target).closest('.comment-card');

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
}