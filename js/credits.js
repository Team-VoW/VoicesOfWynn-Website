$(function () {
    let $rows = $(".row");
    Object.keys($rows).forEach(key => {
        let $cards = $($rows.get(key)).find(".card");
        let currentMaxHeight = 0;
        console.log($cards.length);
        for (let i = 0; i < $cards.length; i++) {
            let $currentCard = $($cards.get(i));
            if ($currentCard.outerHeight() > currentMaxHeight) {
                currentMaxHeight = $currentCard.outerHeight();
            }

            //If we're on the last card of the group
            if (i+1 === $cards.length) {
                let indexOfFirstCardOnCurrentRow = i - (i % 4);
                for (j = indexOfFirstCardOnCurrentRow; j < $cards.length; j++) {
                    $($cards[j].closest(".cardwrapper")).height(currentMaxHeight + "px");
                }
                console.log("Row terminated early: " + currentMaxHeight);
            }

            //If we're on the fourth (last) card in the row
            if (i+1 % 4 === 0) {
                let indexOfFirstCardOnCurrentRow = i - 3;
                for (j = indexOfFirstCardOnCurrentRow; j < i; j++) {
                    $($cards[j].closest(".cardwrapper")).height(currentMaxHeight + "px");
                }
                console.log("Top height in current row: " + currentMaxHeight);
                currentMaxHeight = 0;
            }
        }
    });
});