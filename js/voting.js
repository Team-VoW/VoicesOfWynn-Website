var $clickedRating;
var voteCount;
var voteType;

$(".upvote").on('click', function(event) {
    $clickedRating = $(event.target);
    if ($clickedRating.prop('tagName') !== 'BUTTON') {
        $clickedRating = $clickedRating.closest('button');
    }
    if ($clickedRating.hasClass("clicked")) {
        //Removing upvote
        voteCount = (Number)($clickedRating.find('span').text()) - 1;
        voteType = "remove";
    }
    else {
        voteCount = (Number)($clickedRating.find('span').text()) + 1;
        voteType = "add";
    }

    let recordingId = $clickedRating.attr('data-recording-id');
    let npcId =$clickedRating.attr('data-npc-id');

    $.ajax({
        url: "contents/npc/" + npcId + "/upvote/" + recordingId,
        type: 'POST',
        success: function(result, message) {
            $clickedRating.find('span').text(voteCount);
            $clickedRating.closest('tr').find('button.upvote.clicked,button.downvote.clicked').find('span').text((Number)($clickedRating.closest('tr').find('button.upvote.clicked,button.downvote.clicked').find('span').text()) - 1);
            $clickedRating.closest('tr').find('button.upvote,button.downvote').removeClass('clicked');
            if (voteType === "add")
                $clickedRating.closest('tr').find('button.upvote').addClass('clicked');
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        }
    });
});

$(".downvote").on('click', function(event) {
    $clickedRating = $(event.target);
    if ($clickedRating.prop('tagName') !== 'BUTTON') {
        $clickedRating = $clickedRating.closest('button');
    }
    if ($clickedRating.hasClass("clicked")) {
        //Removing downvote
        voteCount = (Number)($clickedRating.find('span').text()) - 1;
        voteType = "remove";
    }
    else {
        voteCount = (Number)($clickedRating.find('span').text()) + 1;
        voteType = "add";
    }

    let recordingId = $clickedRating.attr('data-recording-id');
    let npcId = $clickedRating.attr('data-npc-id');
    $.ajax({
        url: "contents/npc/" + npcId + "/downvote/" + recordingId,
        type: 'POST',
        success: function(result, message) {
            $clickedRating.find('span').text(voteCount);
            $clickedRating.closest('tr').find('button.upvote.clicked,button.downvote.clicked').find('span').text((Number)($clickedRating.closest('tr').find('button.upvote.clicked,button.downvote.clicked').find('span').text()) - 1);
            $clickedRating.closest('tr').find('button.upvote,button.downvote').removeClass('clicked');
            if (voteType === "add")
                $clickedRating.closest('tr').find('button.downvote').addClass('clicked');
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        }
    });
});