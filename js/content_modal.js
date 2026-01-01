$(function () {
    let $modal = $('#content-modal')
    let $closeButton = $modal.find('.modal__close')
    let $content = $modal.find('#content')
    let $questBoxes = $(".quests-grid").find('.quest-card')
    let $searchbar = $("#q_search")

    // Helper to build NPC HTML with proper storage URLs
    async function buildNpcHtml(npcs) {
        const config = await VoWStorage.getConfig();
        let html = '';
        npcs.forEach(npc => {
            const imgUrl = config.baseUrl + config.paths.npcs + npc.id + '.png';
            html += `
                <div class="npc">
                    <img class="image" src="${imgUrl}" alt="NPC avatar"/>
                    <p class="name"><a href="contents/npc/${npc.id}">${npc.name}</a></p>
                </div>
            `;
        });
        return html;
    }

    $searchbar.keyup(function () {
        $('#results_container').find('.quest-card, .card.q-voice').click(function () {
            $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, async function (data) {
                let npcs = data[0].npcs;
                $content.html(await buildNpcHtml(npcs));
                $modal.css('display','flex');
            })
        })
    })

    $questBoxes.click(function () {
        $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, async function (data) {
            let npcs = data[0].npcs;
            $content.html(await buildNpcHtml(npcs));
            $modal.css('display','flex');
        })
    })

    $closeButton.click(function () {
        $modal.css('display','none')
    })

    // Close modal when clicking outside (on the overlay)
    $modal.click(function(e) {
        if (e.target === this) {
            $modal.css('display','none')
        }
    })
})