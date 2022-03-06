$(function() {
    let $nametags = $(".questName"); //Get all nametags wrapper elements (<p>)
    let nametagCount = $nametags.length;
    for (let i = 0; i < nametagCount; i++) { //Iterate through them
        let $currentNametag = $($nametags[i]);
        let maxWidth = $currentNametag.width(); //Get max width that the nametag can have (should be the same for all)
        let currentFontSize = $currentNametag.css('font-size').substring(0, $currentNametag.css('font-size').length - 2); //Get the current font size (remove "px" from the returned value)
        while (currentWidth > maxWidth) {
            currentFontSize -= 1; //Decrease the font size by 1 px
            $currentNametag.css("font-size", (currentFontSize + "px")); //Apply it to the nametag
            currentWidth = $currentNametag.width(); //Recalculate current width, keep doing this untill the nametag fits
        }
    }
});