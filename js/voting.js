var $clickedRating;

$(".upvote").on('click', function(event) {
    if (!confirm('Upvote this recording?')) { return; }

    $clickedRating = $(event.target);
    if ($clickedRating.prop('tagName') !== 'BUTTON') {
        $clickedRating = $clickedRating.closest('button');
    }
    let recordingId = $clickedRating.attr('data-recording-id');
    let npcId =$clickedRating.attr('data-npc-id');

    $.ajax({
        url: "contents/npc/" + npcId + "/upvote/" + recordingId,
        type: 'POST',
        success: function(result, message) {
            $clickedRating.find('span').text((Number)($clickedRating.find('span').text()) + 1);
            $clickedRating.closest('tr').find('a').off('click');
            $clickedRating.closest('tr').find('button.upvote').prop('disabled', true);
            $clickedRating.closest('tr').find('button.downvote').prop('disabled', true);
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
    if ($clickedRating.prop('tagName') !== 'BUTTON') {
        $clickedRating = $clickedRating.closest('button');
    }
    let recordingId = $clickedRating.attr('data-recording-id');
    let npcId = $clickedRating.attr('data-npc-id');
    console.log(event.target);
    $.ajax({
        url: "contents/npc/" + npcId + "/downvote/" + recordingId,
        type: 'POST',
        success: function(result, message) {
            $clickedRating.find('span').text((Number)($clickedRating.find('span').text()) + 1);
            $clickedRating.closest('tr').find('a').off('click');
            $clickedRating.closest('tr').find('button.upvote').prop('disabled', true);
            $clickedRating.closest('tr').find('button.downvote').prop('disabled', true);
            $clickedRating = undefined;
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
        }
    });
});