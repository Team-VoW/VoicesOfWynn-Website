<?php

namespace VoicesOfWynn\Models\Website;

class FaqItem
{

    private int $id;
    private int $sortingOrder;
    private bool $visible;
    private string $question;
    private string $answer;

    /**
     * @param array $data Data returned from database, invalid items are skipped, multiple key names are supported for
     * each attribute
     */
    public function __construct(array $data)
    {
        foreach ($data as $key => $value) {
            switch ($key) {
                case 'id':
                case 'faq_id':
                    $this->id = $value;
                    break;
                case 'sortingOrder':
                case 'sorting_order':
                case 'order':
                    $this->sortingOrder = $value;
                    break;
                case 'visible':
                case 'displayed':
                case 'active':
                    $this->visible = $value;
                    break;
                case 'question':
                case 'q':
                    $this->question = $value;
                    break;
                case 'answer':
                case 'a':
                    $this->answer = $value;
                    break;
            }
        }
    }

    public function getId() : int
    {
        return $this->id;
    }

    public function getOrder() : int
    {
        return $this->sortingOrder;
    }

    public function isVisible() : bool
    {
        return $this->visible;
    }

    public function getQuestion() : string
    {
        return $this->question;
    }

    public function getAnswer() : string
    {
        return $this->answer;
    }
}
