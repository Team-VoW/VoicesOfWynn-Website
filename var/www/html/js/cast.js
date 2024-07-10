$(function () {
  let $cards = $(".socials-card");
  let currentMaxWidth = 0;
  for (let i = 0; i < $cards.length; i++) 
  { 
    if(currentMaxWidth < Math.ceil($($cards[i]).outerWidth())) 
    {
      currentMaxWidth = Math.ceil($($cards[i]).outerWidth());
      console.log(Math.ceil($($cards[i]).outerWidth()))
    }
    if(i+1 === $cards.length) 
    {
      for(let h = 0; h < $cards.length; h++)
      {
        $($cards[h]).width(currentMaxWidth + "px")
      }
    }
  }
});