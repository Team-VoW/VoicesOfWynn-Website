$(function () {
    let $modal = $('#content-modal')
    let $closeButton = $modal.find('.modal__close')
    let $content = $modal.find('#content')
    let $questBoxes = $(".quests-grid").find('.quest-card')
    let $searchbar = $("#q_search")
    let lastFocusedElement = null

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

    // Get all focusable elements within the modal
    function getFocusableElements() {
        return $modal.find('a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])').filter(':visible');
    }

    // Open modal with accessibility features
    function openModal() {
        lastFocusedElement = document.activeElement;
        $modal.css('display','flex');
        $modal.attr('aria-hidden', 'false');

        // Focus the close button when modal opens
        setTimeout(() => {
            $closeButton.focus();
        }, 100);
    }

    // Close modal with accessibility features
    function closeModal() {
        $modal.css('display','none');
        $modal.attr('aria-hidden', 'true');

        // Restore focus to the element that opened the modal
        if (lastFocusedElement) {
            lastFocusedElement.focus();
        }
    }

    // Focus trap: handle Tab and Shift+Tab
    function handleTabKey(e) {
        const focusableElements = getFocusableElements();
        const firstElement = focusableElements.first()[0];
        const lastElement = focusableElements.last()[0];

        if (e.shiftKey) {
            // Shift + Tab
            if (document.activeElement === firstElement) {
                e.preventDefault();
                lastElement.focus();
            }
        } else {
            // Tab
            if (document.activeElement === lastElement) {
                e.preventDefault();
                firstElement.focus();
            }
        }
    }

    // Keyboard event handler for modal
    function handleKeyDown(e) {
        // Close on Escape key
        if (e.key === 'Escape' || e.keyCode === 27) {
            closeModal();
        }

        // Handle Tab for focus trap
        if (e.key === 'Tab' || e.keyCode === 9) {
            handleTabKey(e);
        }
    }

    $searchbar.keyup(function () {
        $('#results_container').find('.quest-card, .card.q-voice').click(function () {
            $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, async function (data) {
                let npcs = data[0].npcs;
                $content.html(await buildNpcHtml(npcs));
                openModal();
            })
        })
    })

    $questBoxes.click(function () {
        $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, async function (data) {
            let npcs = data[0].npcs;
            $content.html(await buildNpcHtml(npcs));
            openModal();
        })
    })

    $closeButton.click(function () {
        closeModal();
    })

    // Close modal when clicking outside (on the overlay)
    $modal.click(function(e) {
        if (e.target === this) {
            closeModal();
        }
    })

    // Add keyboard event listener to document
    $(document).on('keydown', function(e) {
        if ($modal.css('display') === 'flex') {
            handleKeyDown(e);
        }
    })
})