var $clickedRating;

$(".upvote").on('click', function(event) {
    if (!confirm('Upvote this recording?')) { return; }

    $clickedRating = $(event.target);
    let recordingId = $(event.target).attr('data-recording-id');

    $.ajax({
        url: "contents/npc/" + npcId + "/upvote/" + recordingId,
        type: 'POST',
        success: function(result, message) {
            $clickedRating.text((Number)($clickedRating.text()) + 1);
            $clickedRating.closest('tr').find('a').off('click');
            $clickedRating.closest('tr').find('a.upvote').removeClass('upvote');
            $clickedRating.closest('tr').find('a.downvote').removeClass('downvote');
            $clickedRating = undefined;
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
        }
    });
});

$(".downvote").on('click', function(event) {
    if (!confirm('Downvote this recording?\nPlease, make sure to leave some constructive feedback in a comment 😉')) { return; }

    $clickedRating = $(event.target);
    let recordingId = $clickedRating.attr('data-recording-id');

    $.ajax({
        url: "contents/npc/" + npcId + "/downvote/" + recordingId,
        type: 'POST',
        success: function(result, message) {
            $clickedRating.text((Number)($clickedRating.text()) + 1);
            $clickedRating.closest('tr').find('a').off('click');
            $clickedRating.closest('tr').find('a.upvote').removeClass('upvote');
            $clickedRating.closest('tr').find('a.downvote').removeClass('downvote');
            $clickedRating = undefined;
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
        }
    });
});