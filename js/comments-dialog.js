// Comments Dialog Modal
// Opens an in-page modal with comment form + list for a given NPC

var $commentsDialogOverlay = $('#comments-dialog-overlay');

// --- Open / Close ---

function openCommentsDialog(npcId) {
    var $body = $commentsDialogOverlay.find('.comments-dialog-body');
    $body.html('<p style="text-align:center;padding:2rem;">Loading&hellip;</p>');
    $commentsDialogOverlay.removeAttr('hidden');
    $('body').css('overflow', 'hidden');

    $.ajax({
        url: 'contents/npc/' + npcId + '/comments/dialog',
        type: 'GET',
        success: function (html) {
            $body.html(html);
            initDialogHandlers(npcId);
        },
        error: function () {
            $body.html('<p style="text-align:center;padding:2rem;color:#c00;">Failed to load comments. Please try again.</p>');
        }
    });
}

function closeCommentsDialog() {
    $commentsDialogOverlay.attr('hidden', '');
    $('body').css('overflow', '');
    $commentsDialogOverlay.find('.comments-dialog-body').html('');
}

// --- Trigger ---

$(document).on('click', '.comment-btn', function () {
    var npcId = $(this).data('npc-id');
    openCommentsDialog(npcId);
});

// --- Close on backdrop click or close button ---

$commentsDialogOverlay.on('click', function (e) {
    if ($(e.target).is($commentsDialogOverlay)) {
        closeCommentsDialog();
    }
});

$(document).on('click', '.comments-dialog-close', function () {
    closeCommentsDialog();
});

// --- Close on Escape key ---

$(document).on('keydown', function (e) {
    if (e.key === 'Escape' && !$commentsDialogOverlay.attr('hidden')) {
        closeCommentsDialog();
    }
});

// --- Init handlers after fragment is loaded ---

function initDialogHandlers(npcId) {
    var $overlay = $commentsDialogOverlay;

    // Show/hide form
    $overlay.find('#comments-dialog-new-btn').on('click', function () {
        $overlay.find('#comments-dialog-form-card').slideDown(400);
        $(this).hide();
        $overlay.find('#comments-dialog-hide-btn').show();
    });

    $overlay.find('#comments-dialog-hide-btn').on('click', function () {
        $overlay.find('#comments-dialog-form-card').slideUp(400);
        $(this).hide();
        $overlay.find('#comments-dialog-new-btn').show();
    });

    // Tab switching
    $overlay.find('#comments-dialog-contributor-tab').on('click', function () {
        $(this).addClass('selected');
        $overlay.find('#comments-dialog-guest-tab').removeClass('selected');
        $overlay.find('#comments-dialog-guest-form').hide();
        $overlay.find('#comments-dialog-contributor-form').show();
    });

    $overlay.find('#comments-dialog-guest-tab').on('click', function () {
        $(this).addClass('selected');
        $overlay.find('#comments-dialog-contributor-tab').removeClass('selected');
        $overlay.find('#comments-dialog-contributor-form').hide();
        $overlay.find('#comments-dialog-guest-form').show();
    });

    // Form submit
    $overlay.find('form').on('submit', function (e) {
        e.preventDefault();
        var $form = $(this);
        var formNpcId = $form.data('npc-id');
        var name, email, content, antispam, verified;

        var $contributorTab = $overlay.find('#comments-dialog-contributor-tab');
        if ($contributorTab.length && $contributorTab.hasClass('selected')) {
            verified = true;
            content = $overlay.find('#comments-dialog-content-contributor').val();
        } else {
            verified = false;
            name = $overlay.find('#comments-dialog-name').val();
            email = $overlay.find('#comments-dialog-email').val();
            content = $overlay.find('#comments-dialog-content-guest').val();
            antispam = $overlay.find('#comments-dialog-antispam').val();
        }

        $.ajax({
            url: 'contents/npc/' + formNpcId + '/comments/new',
            type: 'POST',
            data: {
                'verified': verified,
                'name': name,
                'email': email,
                'content': content,
                'antispam': antispam
            },
            success: function (newCommentId) {
                var displayName, badges = '', gravatar, displayContent;

                if (verified) {
                    displayName = "<a href='cast/" + dialogUserId + "'>" + dialogUserName + "</a>";
                    gravatar = dialogUserAvatar;
                    if (dialogUserId == dialogVoiceActorId) {
                        badges = "<span class=\"author-badge\" title=\"This user is voicing this NPC.\">Author</span>";
                    }
                    badges += "\n<span class=\"contributor-badge\" title=\"This user contributed to this project.\">Contributor</span>";
                    displayContent = $overlay.find('#comments-dialog-content-contributor').val().replace(/\n/g, '<br>');
                } else {
                    displayName = $overlay.find('#comments-dialog-name').val() || 'Anonymous';
                    gravatar = 'https://www.gravatar.com/avatar/' + md5($overlay.find('#comments-dialog-email').val()) + '?d=identicon';
                    badges = '';
                    displayContent = $overlay.find('#comments-dialog-content-guest').val().replace(/\n/g, '<br>');
                }

                var commentHTML = dialogCommentItemHTML
                    .replace('{name}', displayName)
                    .replace('{gravatar}', gravatar)
                    .replace('{badges}', badges)
                    .replace('{id}', newCommentId)
                    .replace('{comment}', displayContent);

                var $comment = $(commentHTML);
                $comment.find('.delete-comment-button').on('click', deleteDialogComment);
                $comment.hide();
                $overlay.find('#comments-dialog-list').prepend($comment);
                $comment.fadeIn(800);

                $overlay.find('#comments-dialog-hide-btn').click();
                $overlay.find('#comments-dialog-content-contributor').val('');
                $overlay.find('#comments-dialog-content-guest').val('');
            },
            error: function (xhr, status, error) {
                alert('An error occurred: ' + error);
            }
        });
    });

    // Delete buttons on existing comments
    $overlay.find('.delete-comment-button').on('click', deleteDialogComment);
}

// --- Comment card template ---

var dialogCommentItemHTML = `
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
        <button data-comment-id="{id}" class="delete-comment-button" title="Delete comment">&times;</button>
    </div>
    <hr class="comment-separator">
    <div class="comment-content">
        <p>{comment}</p>
    </div>
</div>
`;

// --- Delete comment ---

function deleteDialogComment(event) {
    if (!confirm('Do you really want to delete this comment?')) {
        return;
    }

    var $card = $(event.target).closest('.comment-card');
    var commentId = $(event.target).attr('data-comment-id');

    $.ajax({
        url: 'contents/npc/' + dialogNpcId + '/comments/delete/' + commentId,
        type: 'DELETE',
        success: function () {
            $card.slideUp(500, function () { $card.remove(); });
        },
        error: function (xhr, status, error) {
            alert('An error occurred: ' + error);
        }
    });
}
