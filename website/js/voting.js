var $clickedRating;
var voteCount;
var voteType;

$(".upvote").on('click', function (event) {
    $clickedRating = $(event.target);
    if ($clickedRating.prop('tagName') !== 'BUTTON') {
        $clickedRating = $clickedRating.closest('button');
    }
    if ($clickedRating.hasClass("clicked")) {
        //Removing upvote
        voteCount = (Number)($clickedRating.find('.vote-count').text())// - 1; //Is removed in the callback (from all .clicked buttons)
        voteType = "remove";
    }
    else {
        voteCount = (Number)($clickedRating.find('.vote-count').text()) + 1;
        voteType = "add";
    }

    let npcId = $clickedRating.attr('data-npc-id');

    $.ajax({
        url: "contents/npc/" + npcId + "/upvote",
        type: 'POST',
        data: uuid ? "uuid=" + uuid : {},
        success: function (result, message) {
            $clickedRating.find('.vote-count').text(voteCount);
            const $box = $clickedRating.closest('.recording-actions');
            const $clickedButtons = $box.find('button.upvote.clicked,button.downvote.clicked');
            $clickedButtons.find('.vote-count').text(Number($clickedButtons.find('.vote-count').text()) - 1);
            $box.find('button.upvote,button.downvote').removeClass('clicked');
            if (voteType === "add")
                $box.find('button.upvote').addClass('clicked');
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        },
        error: function (result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        }
    });
});

$(".downvote").on('click', function (event) {
    $clickedRating = $(event.target);
    if ($clickedRating.prop('tagName') !== 'BUTTON') {
        $clickedRating = $clickedRating.closest('button');
    }
    if ($clickedRating.hasClass("clicked")) {
        //Removing downvote
        voteCount = (Number)($clickedRating.find('.vote-count').text())// - 1; //Is removed in the callback (from all .clicked buttons)
        voteType = "remove";
    }
    else {
        voteCount = (Number)($clickedRating.find('.vote-count').text()) + 1;
        voteType = "add";
    }

    let npcId = $clickedRating.attr('data-npc-id');
    $.ajax({
        url: "contents/npc/" + npcId + "/downvote",
        type: 'POST',
        data: uuid ? "uuid=" + uuid : {},
        success: function (result, message) {
            $clickedRating.find('.vote-count').text(voteCount);
            const $box = $clickedRating.closest('.recording-actions');
            const $clickedButtons = $box.find('button.upvote.clicked,button.downvote.clicked');
            $clickedButtons.find('.vote-count').text(Number($clickedButtons.find('.vote-count').text()) - 1);
            $box.find('button.upvote,button.downvote').removeClass('clicked');
            if (voteType === "add")
                $box.find('button.downvote').addClass('clicked');
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        },
        error: function (result, message, error) {
            alert("An error occurred: " + error);
            $clickedRating = undefined;
            voteCount = undefined;
            voteType = undefined;
        }
    });
});