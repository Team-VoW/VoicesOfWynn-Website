$(function () {
    let $groups = $(".row");
    for (let h = 0; h < $groups.length; h++) {
        let $cards = $($groups.get(h)).find(".card");
        let currentMaxHeight = 0;
        for (let i = 0; i < $cards.length; i++) {
            let $currentCard = $($cards.get(i));

            console.log($currentCard.find("strong").text() + ":" + $currentCard.outerHeight())
            if ($currentCard.outerHeight() > currentMaxHeight) {
                currentMaxHeight = $currentCard.outerHeight();
            }

            //If we're on the last card of the group
            if (i+1 === $cards.length) {
                let indexOfFirstCardOnCurrentRow = i - (i % 4);
                for (j = indexOfFirstCardOnCurrentRow; j < $cards.length; j++) {
                    $($cards[j].closest(".cardwrapper")).height(currentMaxHeight + "px");
                }
            }

            //If we're on the fourth (last) card in the row
            if ((i+1) % 4 === 0) {
                let indexOfFirstCardOnCurrentRow = i - 3;
                for (j = indexOfFirstCardOnCurrentRow; j <= i; j++) {
                    $($cards[j].closest(".cardwrapper")).height(currentMaxHeight + "px");
                }
                currentMaxHeight = 0;
            }
        }
    }
});