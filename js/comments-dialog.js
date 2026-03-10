// Comments Dialog Modal
// Opens an in-page modal with comment form + list for a given NPC

var $commentsDialogOverlay = $('#comments-dialog-overlay');

// Stores the element that triggered the dialog so focus can be restored on close
var $dialogTrigger = null;

// Escapes user-supplied strings before inserting them into HTML to prevent XSS.
// Server-side sanitize() only runs on data that has been round-tripped through
// the server; client-side form values that are optimistically rendered never
// pass through it, so they must be escaped here.
function escapeHtml(str) {
    return $('<div>').text(String(str)).html();
}

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
            // Move focus into the modal so keyboard/screen-reader users don't
            // stay on (or navigate behind) the now-hidden page content
            $commentsDialogOverlay.find('.comments-dialog-close').focus();
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
    // Return focus to whichever element opened the dialog so keyboard users
    // aren't dropped back to the top of the page
    if ($dialogTrigger) {
        $dialogTrigger.focus();
        $dialogTrigger = null;
    }
}

// --- Trigger ---

$(document).on('click', '.comment-btn', function () {
    // Store the button so focus can be returned to it when the dialog closes
    $dialogTrigger = $(this);
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

// --- Focus trap: keep Tab cycling inside the modal while it is open ---
// Without this, keyboard users can Tab through the background page behind the overlay.
$commentsDialogOverlay.on('keydown', function (e) {
    // Only intercept Tab when the overlay is visible (no 'hidden' attribute)
    if (e.key !== 'Tab' || $commentsDialogOverlay.attr('hidden') !== undefined) return;
    var focusable = $commentsDialogOverlay
        .find('a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])')
        .filter(':visible');
    if (!focusable.length) return;
    var first = focusable.first()[0];
    var last = focusable.last()[0];
    if (e.shiftKey && document.activeElement === first) {
        // Shift+Tab on the first element: wrap around to the last
        e.preventDefault();
        last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
        // Tab on the last element: wrap around to the first
        e.preventDefault();
        first.focus();
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
                // Keep the comment count badge on the trigger button in sync
                var $count = $('.comment-btn[data-npc-id="' + formNpcId + '"] .vote-count');
                $count.text(Number($count.text() || 0) + 1);

                var displayName, badges = '', gravatar, displayContent;

                if (verified) {
                    // dialogUserName comes via the server sanitize() path, but we
                    // escapeHtml it anyway for defence-in-depth when inserting into HTML
                    displayName = "<a href='cast/" + dialogUserId + "'>" + escapeHtml(dialogUserName) + "</a>";
                    gravatar = dialogUserAvatar;
                    if (dialogUserId == dialogVoiceActorId) {
                        badges = "<span class=\"author-badge\" title=\"This user is voicing this NPC.\">Author</span>";
                    }
                    badges += "\n<span class=\"contributor-badge\" title=\"This user contributed to this project.\">Contributor</span>";
                    // Escape raw textarea value before converting newlines to <br> tags;
                    // .val() is never touched by server-side sanitize() in the optimistic render
                    displayContent = escapeHtml($overlay.find('#comments-dialog-content-contributor').val()).replace(/\n/g, '<br>');
                } else {
                    // Guest name comes directly from the form — never touched the server sanitizer
                    displayName = escapeHtml($overlay.find('#comments-dialog-name').val() || 'Anonymous');
                    gravatar = 'https://www.gravatar.com/avatar/' + md5($overlay.find('#comments-dialog-email').val()) + '?d=identicon';
                    badges = '';
                    // Escape raw textarea value before converting newlines to <br> tags
                    displayContent = escapeHtml($overlay.find('#comments-dialog-content-guest').val()).replace(/\n/g, '<br>');
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
            // Keep the comment count badge on the trigger button in sync
            var $count = $('.comment-btn[data-npc-id="' + dialogNpcId + '"] .vote-count');
            $count.text(Math.max(0, Number($count.text() || 0) - 1));
            $card.slideUp(500, function () { $card.remove(); });
        },
        error: function (xhr, status, error) {
            alert('An error occurred: ' + error);
        }
    });
}
